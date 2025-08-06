// Định nghĩa hàm global với tham số connection và memberKey
async function startConnection(connection, memberKey) {
    try {
        console.log("Checking connection:", connection.state);
        if (connection.state !== signalR.HubConnectionState.Disconnected) {
            await connection.stop();
        }
        await connection.start();
        console.log("[startConnection] Connected to ChatHub successfully");
        await connection.invoke("InitializeConnection", null, memberKey);
        connection.on('ReceiveMessage', updateUnreadCount);
        connection.on('Disconnected', () => {
            console.log("[Disconnected] Connection lost, attempting reconnect");
            setTimeout(() => startConnection(connection, memberKey), 2000);
        });
        connection.on("ReloadConversations", async (conversationKey) => {
            console.log(`Received ReloadConversations for conversation: ${conversationKey}`);
            if (typeof loadConversations === 'function') {
                await loadConversations();
            }
            if (currentConversationKey === conversationKey && typeof loadMessages === 'function') {
                await loadMessages(conversationKey, false, 0);
            }
        });
    } catch (err) {
        console.error("[startConnection] Connection failed:", err);
        setTimeout(() => startConnection(connection, memberKey), 5000);
    }
}

// Hàm updateUnreadCount cần được định nghĩa trước khi sử dụng
function updateUnreadCount(count) {
    const badge = document.getElementById("unreadCount");
    if (badge) {
        badge.textContent = count;
        badge.classList.toggle("d-none", count === 0);
    }
}

// Biến toàn cục
let unreadInterval;

document.addEventListener("DOMContentLoaded", async () => {
    if (!window.signalR) {
        console.error("[DOMContentLoaded] SignalR not loaded!");
        return;
    }

    let unreadCount = 0;
    let selectedFile = null;
    let currentConversationKey = null;
    let currentUserKey = null;
    let currentUserType = null;
    let currentConversationType = null;
    let skip = 0;
    let allMessages = [];
    let memberKey = null;

    try {
        const response = await fetch('/api/conversations/GetMemberKey', {
            method: 'GET',
            credentials: 'include'
        });
        if (response.ok) memberKey = await response.text();
        else console.warn("[DOMContentLoaded] Failed to get MemberKey:", await response.text());
    } catch (error) {
        console.error("[DOMContentLoaded] Error fetching MemberKey:", error);
    }

    if (!memberKey) {
        console.warn("[DOMContentLoaded] MemberKey not found. Disabling chat.");
        document.getElementById("openChat")?.addEventListener("click", () => alert("Please log in."));
        return;
    }

    // Định nghĩa toàn cục
    window.connection = new signalR.HubConnectionBuilder()
        .withUrl("https://localhost:7003/chatHub")
        .withAutomaticReconnect()
        .build();
    window.loadConversations = loadConversations;

    const openChat = document.getElementById("openChat");
    const closeChat = document.getElementById("closeChat");
    const chatModal = document.getElementById("chatModal");
    const chatInput = document.getElementById("chatInput");
    const fileInput = document.getElementById("fileInput");
    const fileIcon = document.getElementById("fileIcon");
    const sendIcon = document.getElementById("sendIcon");
    const filePreviewContainer = document.getElementById("filePreviewContainer");
    const filePreview = document.getElementById("filePreview");
    const videoPreview = document.getElementById("videoPreview");
    const audioPreview = document.getElementById("audioPreview");
    const clearFile = document.getElementById("clearFile");
    const messageList = document.getElementById("messageList");
    const conversationListContainer = document.getElementById("conversationListContainer");
    const conversationList = document.getElementById("conversationList");
    const searchInput = document.getElementById("searchInput");
    const searchResults = document.getElementById("searchResults");
    const chatHeaderInfo = document.getElementById("chatHeaderInfo");
    const chatHeaderContent = document.getElementById("chatHeaderContent");
    const headerAvatar = document.getElementById("headerAvatar");
    const headerName = document.getElementById("headerName");
    const pinnedSection = document.getElementById("pinnedSection");
    const pinnedPopup = document.getElementById("pinnedPopup");
    const pinnedContent = document.getElementById("pinnedContent");

    let blockPopup = null;

    // Polling unread count
    async function updateUnreadCountInitial() {
        try {
            const response = await fetch('/api/conversations/GetUnthread');
            if (!response.ok) {
                if (response.status === 401) return;
                throw new Error('API failed');
            }
            const data = await response.json();
            unreadCount = data.totalUnreadCount || 0;
            updateUnreadCount(unreadCount);
        } catch (err) {
            console.debug('Update unread count skipped:', err);
        }
    }

    if (window.isAuthenticated) {
        unreadInterval = setInterval(updateUnreadCountInitial, 60000);
        updateUnreadCountInitial();
    }

    function debounce(func, wait) {
        let timeout;
        return (...args) => {
            clearTimeout(timeout);
            timeout = setTimeout(() => func.apply(this, args), wait);
        };
    }

    function attachIconListeners() {
        const blockIcon = document.getElementById("iconBlock");
        if (!blockIcon) {
            console.warn("[attachIconListeners] IconBlock not found");
            return;
        }

        blockIcon.replaceWith(blockIcon.cloneNode(true));
        const newBlockIcon = document.getElementById("iconBlock");
        newBlockIcon.classList.add("icon-hover");
        newBlockIcon.style.cursor = "pointer";

        newBlockIcon.addEventListener("mouseenter", () => console.log(`[IconHover] Hover on iconBlock at:`, new Date().toISOString()));
        newBlockIcon.addEventListener("mouseleave", () => console.log(`[IconHover] Mouse left iconBlock at:`, new Date().toISOString()));

        newBlockIcon.addEventListener("click", debounce((e) => {
            if (blockPopup) {
                blockPopup.remove();
                blockPopup = null;
                return;
            }

            blockPopup = document.createElement("div");
            blockPopup.id = "blockPopup";
            blockPopup.innerHTML = `
                <div class="popup-dialog">
                    <p class="popup-title">What would you like to do?</p>
                    <button class="btn btn-danger w-100 mb-2" id="btnDeleteConversation">Delete Conversation</button>
                    <button class="btn btn-warning w-100 mb-2" id="btnBlockUser">Block</button>
                    <button class="btn btn-secondary w-100" id="btnCancelBlock">Cancel</button>
                </div>
            `;
            Object.assign(blockPopup.style, {
                position: "absolute",
                top: "60px",
                right: "20px",
                zIndex: "1050",
                background: "#222",
                padding: "16px",
                borderRadius: "12px",
                boxShadow: "0 4px 8px rgba(0,0,0,0.2)",
                minWidth: "200px",
                color: "white",
                pointerEvents: "auto"
            });
            chatModal.appendChild(blockPopup);

            document.getElementById("btnDeleteConversation").addEventListener("click", async () => {
                try {
                    await fetch(`/api/ChatController/DeleteConversation/${currentConversationKey}`, { method: 'POST', credentials: 'include' });
                    resetChatInterface();
                    loadConversations();
                } catch (err) {
                    console.error("[blockPopup] Error deleting conversation:", err);
                }
                blockPopup.remove();
                blockPopup = null;
            });

            document.getElementById("btnBlockUser").addEventListener("click", async () => {
                try {
                    await fetch(`/api/ChatController/BlockUser/${currentConversationKey}`, { method: 'POST', credentials: 'include' });
                    resetChatInterface();
                    loadConversations();
                } catch (err) {
                    console.error("[blockPopup] Error blocking user:", err);
                }
                blockPopup.remove();
                blockPopup = null;
            });

            document.getElementById("btnCancelBlock").addEventListener("click", () => {
                blockPopup.remove();
                blockPopup = null;
            });

            const outsideClickHandler = (event) => {
                if (blockPopup && !blockPopup.contains(event.target) && event.target !== newBlockIcon && !newBlockIcon.contains(event.target)) {
                    blockPopup.remove();
                    blockPopup = null;
                    document.removeEventListener("click", outsideClickHandler);
                }
            };
            setTimeout(() => document.addEventListener("click", outsideClickHandler), 100);
        }, 100));

        const iconIds = ["iconCall", "iconVideo", "iconSetting"];
        iconIds.forEach(id => {
            const el = document.getElementById(id);
            if (el) {
                el.classList.add("icon-hover");
                el.style.cursor = "pointer";
                el.addEventListener("click", (e) => console.log(`[IconClick] Clicked ${id} at:`, new Date().toISOString()));
                el.addEventListener("mouseenter", () => console.log(`[IconHover] Hover on ${id} at:`, new Date().toISOString()));
                el.addEventListener("mouseleave", () => console.log(`[IconHover] Mouse left ${id} at:`, new Date().toISOString()));
            } else {
                console.warn(`[attachIconListeners] Element with ID ${id} not found`);
            }
        });
    }

    function updateIconsVisibility() {
        const blockIcon = document.getElementById("iconBlock");
        const settingIcon = document.getElementById("iconSetting");
        if (blockIcon && settingIcon) {
            if (currentConversationType === "Private") {
                blockIcon.style.display = "inline-block";
                settingIcon.style.display = "none";
            } else if (currentConversationType === "Group") {
                blockIcon.style.display = "none";
                settingIcon.style.display = "inline-block";
                settingIcon.replaceWith(settingIcon.cloneNode(true));
                const newSettingIcon = document.getElementById("iconSetting");
                newSettingIcon.classList.add("icon-hover");
                newSettingIcon.style.cursor = "pointer";
                newSettingIcon.addEventListener("click", (event) => {
                    event.stopPropagation();
                    console.log("[Click Setting] currentConversationKey:", currentConversationKey, "showGroupDetails:", typeof window.showGroupDetails);
                    if (typeof window.showGroupDetails === 'function' && currentConversationKey) {
                        window.showGroupDetails(currentConversationKey);
                    } else {
                        console.error("[updateIconsVisibility] showGroupDetails not defined or currentConversationKey is null");
                    }
                });
            } else {
                blockIcon.style.display = "none";
                settingIcon.style.display = "none";
            }
            attachIconListeners();
        } else {
            console.error("[updateIconsVisibility] IconBlock or iconSetting not found");
        }
    }

    function resetFileInput() {
        fileInput.value = "";
        selectedFile = null;
        filePreviewContainer.style.display = "none";
        filePreview.classList.add("d-none");
        videoPreview.classList.add("d-none");
        audioPreview.classList.add("d-none");
        fileInput.disabled = false;
    }

    function resetChatInterface() {
        currentConversationKey = null;
        currentUserKey = null;
        currentUserType = null;
        currentConversationType = null;
        skip = 0;
        allMessages = [];
        messageList.innerHTML = "";
        chatHeaderInfo.style.display = "none";
        chatHeaderContent.style.display = "none";
        pinnedSection.style.display = "none";
        document.querySelectorAll(".conversation-item").forEach(i => i.parentElement.classList.remove("active"));
        updateIconsVisibility();
    }

    function updatePinnedSection() {
        const pinnedMessages = allMessages.filter(m => m.IsPinned).sort((a, b) => new Date(b.CreatedOn) - new Date(a.CreatedOn));
        const firstPinned = pinnedMessages[0];
        const headerText = firstPinned ? (firstPinned.Content || `Pinned ${firstPinned.MessageType || 'Item'}`) : "No pinned messages";
        pinnedSection.innerHTML = `<p>${headerText} (${pinnedMessages.length}/3 pinned)</p>`;
        pinnedSection.style.display = pinnedMessages.length > 0 ? "block" : "none";
    }

    function formatTime(createdOn) {
        const now = new Date();
        const diffMs = now - new Date(createdOn);
        const diffMins = Math.floor(diffMs / 60000);
        const diffHours = Math.floor(diffMs / 3600000);
        const diffDays = Math.floor(diffMs / 86400000);

        return diffDays === 0 ? (diffMins < 60 ? `${diffMins} minutes ago` : `${diffHours} hours ago`) :
            new Date(createdOn).toLocaleString("en-US", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" });
    }

    async function loadConversations() {
        try {
            const response = await fetch(`/api/conversations?memberKey=${encodeURIComponent(memberKey)}`);
            if (!response.ok) throw new Error(`[loadConversations] API failed: ${await response.text()} (Status: ${response.status})`);
            const { conversations } = await response.json();
            conversationList.innerHTML = "";
            conversations.sort((a, b) => {
                const timeA = a.LastMessageTime ? new Date(a.LastMessageTime) : new Date(0);
                const timeB = b.LastMessageTime ? new Date(b.LastMessageTime) : new Date(0);
                return timeB - timeA;
            });
            conversations.forEach(conv => {
                const li = document.createElement("li");
                li.className = "p-2 border-bottom border-white border-opacity-25";
                let lastMessage = conv.LastMessage || "No messages";
                if (conv.ConversationType === "Group" && conv.LastMessage && allMessages.length > 0) {
                    const lastMsgObj = allMessages.find(m => m.ConversationKey === conv.ConversationKey && new Date(m.CreatedOn) >= new Date(conv.LastMessageTime));
                    if (lastMsgObj && lastMsgObj.IsSystemMessage === true) {
                        lastMessage = "No messages";
                    }
                }
                li.innerHTML = `
                    <a href="#" class="d-flex justify-content-between text-white conversation-item" 
                        data-conversation-key="${conv.ConversationKey}" 
                        data-user-key="${conv.ConversationType !== 'Group' ? (conv.PartnerUserKey || '') : ''}" 
                        data-user-type="${conv.ConversationType !== 'Group' ? (conv.PartnerUserType || '') : ''}"
                        data-conversation-type="${conv.ConversationType || ''}">
                        <div class="d-flex">
                            <img src="${conv.Avatar || '/images/avatar/default-avatar.jpg'}" alt="avatar" class="rounded-circle me-3" style="width: 48px; height: 48px;">
                            <div><p class="fw-bold mb-0">${conv.DisplayName || "Unknown"}</p><p class="small mb-0">${lastMessage}</p></div>
                        </div>
                        <div class="text-end"><p class="small mb-1">${conv.LastMessageTime ? formatTime(conv.LastMessageTime) : ""}</p>${conv.UnreadCount > 0 ? `<span class="badge bg-danger rounded-pill px-2">${conv.UnreadCount}</span>` : ''}</div>
                    </a>
                `;
                conversationList.appendChild(li);
            });
            addConversationClickListeners();
        } catch (err) {
            console.error("[loadConversations] Error loading conversations:", err);
        }
    }

    async function searchContacts(query) {
        if (!query) {
            searchResults.innerHTML = "";
            searchResults.classList.remove("show");
            return;
        }
        try {
            const response = await fetch(`/api/conversations/search?query=${encodeURIComponent(query)}&memberKey=${encodeURIComponent(memberKey)}`);
            if (!response.ok) throw new Error(`[searchContacts] Search failed: ${await response.text()} (Status: ${response.status})`);
            const results = await response.json();
            searchResults.innerHTML = results.length === 0 ? `<div class="no-results">No results found</div>` : results.map(result => `
                <div class="search-result-item" data-contact='${JSON.stringify(result).replace(/'/g, "\\'")}' onmousedown="event.preventDefault()">
                    <img src="${result.Avatar || '/images/avatar/default-avatar.jpg'}" alt="${result.Name}" class="rounded-circle">
                    <p>${result.Name}</p>
                </div>
            `).join("");
            searchResults.classList.add("show");
            document.querySelectorAll(".search-result-item").forEach(item => {
                item.addEventListener("click", () => selectContact(JSON.parse(item.getAttribute("data-contact"))));
            });
        } catch (err) {
            console.error("[searchContacts] Error searching:", err);
            searchResults.innerHTML = `<div class="no-results">No results found</div>`;
            searchResults.classList.add("show");
        }
    }

    function selectContact(contact) {
        currentConversationKey = contact.ConversationKey || null;
        currentUserKey = contact.UserKey || null;
        currentUserType = contact.UserType || null;
        currentConversationType = contact.ConversationType || null;
        headerAvatar.src = contact.Avatar || '/images/avatar/default-avatar.jpg';
        headerName.textContent = contact.Name || "Unknown";
        chatHeaderInfo.style.display = "flex";
        chatHeaderContent.style.display = "block";
        messageList.innerHTML = "";
        searchInput.value = "";
        searchResults.classList.remove("show");
        conversationListContainer.classList.remove("focused");
        skip = 0;
        allMessages = [];

        document.querySelectorAll(".conversation-item").forEach(i => i.parentElement.classList.remove("active"));
        if (currentConversationKey) {
            const matchingConv = document.querySelector(`.conversation-item[data-conversation-key="${currentConversationKey}"]`);
            if (matchingConv) matchingConv.parentElement.classList.add("active");
            loadMessages(currentConversationKey, false, skip);
        }

        updatePinnedSection();
        updateIconsVisibility();
    }

    function addConversationClickListeners() {
        document.querySelectorAll(".conversation-item").forEach(item => {
            item.addEventListener("click", (e) => {
                e.preventDefault();
                const conversationKey = item.getAttribute("data-conversation-key");
                const userKey = item.getAttribute("data-user-key") || null;
                const userType = item.getAttribute("data-user-type") || null;
                const conversationType = item.getAttribute("data-conversation-type") || null;

                currentConversationKey = conversationKey;
                currentUserKey = userKey;
                currentUserType = userType;
                currentConversationType = conversationType;

                const conv = item.closest("li").querySelector(".conversation-item");
                headerAvatar.src = conv.querySelector("img").src;
                headerName.textContent = conv.querySelector("p.fw-bold").textContent;
                chatHeaderInfo.style.display = "flex";
                chatHeaderContent.style.display = "block";
                document.querySelectorAll(".conversation-item").forEach(i => i.parentElement.classList.remove("active"));
                item.parentElement.classList.add("active");
                messageList.innerHTML = "";
                skip = 0;
                allMessages = [];

                updatePinnedSection();
                loadMessages(currentConversationKey, false, skip);
                updateIconsVisibility();
            });
        });
    }

    async function loadMessages(conversationKey, append = false, skip) {
        if (!conversationKey) return;
        const url = `/api/conversations/messages/${conversationKey}?skip=${skip}&memberKey=${encodeURIComponent(memberKey)}`;
        try {
            const response = await fetch(url);
            if (!response.ok) throw new Error(`[loadMessages] Failed to load messages: ${await response.text()}`);
            const newMessages = await response.json();
            if (newMessages.length === 0) return;

            newMessages.reverse();

            if (append) {
                allMessages = [...newMessages, ...allMessages];
                const fragment = document.createDocumentFragment();
                newMessages.forEach(m => {
                    const tempDiv = document.createElement('div');
                    tempDiv.innerHTML = addMessage(m);
                    fragment.appendChild(tempDiv.firstChild);
                });
                const prevScrollHeight = messageList.scrollHeight;
                messageList.prepend(fragment);
                messageList.scrollTop = messageList.scrollHeight - prevScrollHeight;
            } else {
                allMessages = [...newMessages];
                messageList.innerHTML = allMessages.map(m => addMessage(m)).join("");
                setTimeout(() => {
                    const container = document.getElementById("messageListContainer");
                    container.scrollTop = container.scrollHeight;
                }, 0);
            }

            skip += newMessages.length;
            updatePinnedSection();
        } catch (err) {
            console.error("[loadMessages] Error loading messages:", err);
        }
    }

    async function loadMessageUntilFound(messageKey, skip) {
        let currentSkip = skip;
        while (true) {
            const url = `/api/conversations/messages/${currentConversationKey}?skip=${currentSkip}&memberKey=${encodeURIComponent(memberKey)}`;
            const response = await fetch(url);
            if (!response.ok) throw new Error(`[loadMessageUntilFound] Failed to load messages: ${await response.text()}`);
            const newMessages = await response.json();
            if (newMessages.length === 0) break;
            newMessages.reverse();
            allMessages = [...newMessages, ...allMessages];
            messageList.innerHTML = allMessages.map(m => addMessage(m)).join("");
            const foundMessage = allMessages.find(m => m.MessageKey === messageKey);
            if (foundMessage) {
                const messageElement = document.querySelector(`[data-message-key="${messageKey}"]`);
                if (messageElement) messageElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
                break;
            }
            currentSkip += 100;
        }
    }

    function showPinnedPopup() {
        if (!currentConversationKey) {
            pinnedPopup.style.display = "none";
            return;
        }

        const pinnedMessages = allMessages.filter(m => m.IsPinned);
        pinnedContent.innerHTML = pinnedMessages.map(m => {
            const isOwn = m.SenderKey === memberKey;
            const contentHtml = m.Content ? `<p class="content" data-message-key="${m.MessageKey}">${m.Content}</p>` : '<p class="content" data-message-key="${m.MessageKey}">No content</p>';

            return `
                <div>
                    <div class="pinned-message-container">
                        <div class="pinned-message ${isOwn ? 'right' : ''}">
                            <div class="message-box">
                                ${contentHtml}
                            </div>
                        </div>
                        <button class="pinned-unpin-btn" data-message-key="${m.MessageKey}">Unpin</button>
                    </div>
                </div>
            `;
        }).join("") || "<div>No pinned messages</div>";

        pinnedPopup.style.display = "block";

        document.addEventListener("click", function hidePopup(e) {
            if (!pinnedPopup.contains(e.target) && e.target !== pinnedSection) {
                pinnedPopup.style.display = "none";
                document.removeEventListener("click", hidePopup);
            }
        });

        pinnedPopup.addEventListener("click", async (e) => {
            if (e.target.classList.contains("pinned-unpin-btn")) {
                const messageKey = e.target.getAttribute("data-message-key");
                await window.connection.invoke("UpdateUnpinStatus", currentConversationKey, messageKey);

                allMessages = allMessages.map(m => m.MessageKey === messageKey ? { ...m, IsPinned: false } : m);
                updatePinnedSection();
                showPinnedPopup();
            } else if (e.target.classList.contains("content")) {
                const messageKey = e.target.getAttribute("data-message-key");
                pinnedPopup.style.display = "none";
                const existingMessage = allMessages.find(m => m.MessageKey === messageKey);
                if (existingMessage) {
                    const messageElement = document.querySelector(`[data-message-key="${messageKey}"]`);
                    if (messageElement) messageElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
                } else {
                    loadMessageUntilFound(messageKey, skip);
                }
            }
        });
    }

    function addMessage(message) {
        const isOwn = message.SenderKey === memberKey;
        const isRecalled = message.Status === 2;
        const isSystem = message.IsSystemMessage === true;
        console.log("Message:", message.MessageKey, "IsSystemMessage:", isSystem);
        const senderName = isOwn ? "You" : (message.SenderName || "Unknown");
        const senderAvatar = isOwn ? "" : `<img src="${message.SenderAvatar || '/images/avatar/default-avatar.jpg'}" class="avatar">`;
        const time = formatTime(message.CreatedOn);
        const status = isOwn ? (message.Status === 0 ? '✔' : '✔✔') : '';

        let html = `<li class="message ${isOwn ? 'right' : 'left'} ${message.MessageType ? 'with-attachment' : ''} ${isRecalled ? 'recalled' : ''} ${isSystem ? 'system-message' : ''}" data-message-key="${message.MessageKey}" data-sender-key="${message.SenderKey}">`;

        if (isSystem) {
            html += `
                <div class="message-box system-box" id="system-message-box">
                    <p class="content">${isRecalled ? "Message recalled" : (message.Content || "")}</p>
                </div>
                <div class="message-timestamp">
                    <span class="time">${time}</span>
                </div>
            `;
        } else {
            html += senderAvatar;
            html += `<div class="message-box">`;
            if (message.ParentMessageKey && message.ParentContent && typeof message.ParentContent === 'string' && message.ParentContent !== "[object Object]" && message.ParentContent.trim() !== "") {
                const displayParent = message.ParentStatus === 2 ? "Message recalled" : (message.ParentContent === "Message recalled" ? "Message recalled" : message.ParentContent);
                html += `<div class="parent-message" data-parent-key="${message.ParentMessageKey}"><p class="content">${displayParent}</p></div>`;
            }
            html += `<div class="message-options"><i class="fas fa-ellipsis-h"></i></div>`;
            if (!isOwn && currentConversationType === 'Group') {
                html += `<p class="name">${senderName}</p><hr>`;
            }
            html += `<p class="content">${isRecalled ? "Message recalled" : (message.Content || "")}</p>`;
            html += `</div>`;
            if (!isRecalled && message.MessageType && message.Url) {
                html += `<div class="attachment">`;
                if (message.MessageType === "Image") {
                    html += `<img src="${message.Url}" class="attachment-media" alt="${message.FileName}">`;
                } else if (message.MessageType === "Audio") {
                    html += `<audio controls><source src="${message.Url}" type="${message.MimeType}"></audio>`;
                } else if (message.MessageType === "Video") {
                    html += `<video controls><source src="${message.Url}" type="${message.MimeType}"></video>`;
                }
                html += `</div>`;
            }
            html += `
                <div class="message-timestamp">
                    <span class="time">${time}</span>
                    ${isOwn ? `<span class="status">${status}</span>` : ''}
                </div>
            `;
        }

        html += `</li>`;
        return html;
    }

    document.addEventListener("click", function (e) {
        const parentEl = e.target.closest(".parent-message");
        if (parentEl) {
            const parentKey = parentEl.getAttribute("data-parent-key");
            const targetEl = document.querySelector(`[data-message-key="${parentKey}"]`);
            if (targetEl) targetEl.scrollIntoView({ behavior: "smooth", block: "center" });
            else loadMessageUntilFound(parentKey, skip);
        }
    });

    messageList.addEventListener("scroll", debounce(() => {
        if (messageList.scrollTop === 0 && currentConversationKey) {
            loadMessages(currentConversationKey, true, skip);
        }
    }, 300));

    window.connection.on("ReceiveMessage", (message) => {
        allMessages.push(message);
        if (currentConversationKey && message.ConversationKey === currentConversationKey) {
            messageList.insertAdjacentHTML("beforeend", addMessage(message));
            messageList.scrollTop = messageList.scrollHeight;
        } else {
            const convItem = document.querySelector(`.conversation-item[data-conversation-key="${message.ConversationKey}"]`);
            if (convItem) {
                const lastMessageEl = convItem.querySelector("p.small.mb-0");
                const timeEl = convItem.querySelector("p.small.mb-1");
                lastMessageEl.textContent = message.Content || "New message";
                timeEl.textContent = formatTime(message.CreatedOn);
                const unreadBadge = convItem.querySelector(".badge");
                if (unreadBadge) unreadBadge.textContent = parseInt(unreadBadge.textContent) + 1 || 1;
                else {
                    const newBadge = document.createElement("span");
                    newBadge.className = "badge bg-danger rounded-pill px-2";
                    newBadge.textContent = "1";
                    convItem.querySelector(".text-end").appendChild(newBadge);
                }
                unreadCount++;
                updateUnreadCount(unreadCount);
            }
        }
        updatePinnedSection();
    });

    window.connection.on("PinResponse", (conversationKey, messageKey, isPinned, success, message) => {
        if (success) {
            const msg = allMessages.find(m => m.MessageKey === messageKey);
            if (msg) msg.IsPinned = isPinned;
            messageList.innerHTML = allMessages.map(m => addMessage(m)).join("");
            messageList.scrollTop = messageList.scrollHeight;
            updatePinnedSection();
        } else {
            console.error("[PinResponse] Failed:", message);
            alert(message || "Pinning failed.");
        }
    });

    window.connection.on("UnpinResponse", (conversationKey, messageKey, success, message) => {
        if (success) {
            const msg = allMessages.find(m => m.MessageKey === messageKey);
            if (msg) msg.IsPinned = false;
            messageList.innerHTML = allMessages.map(m => addMessage(m)).join("");
            messageList.scrollTop = messageList.scrollHeight;
            updatePinnedSection();
        } else {
            console.error("[UnpinResponse] Failed:", message);
            alert(message || "Unpinning failed.");
        }
    });

    window.connection.on("RecallResponse", async (conversationKey, messageKey, success, message) => {
        if (success) {
            let msg = allMessages.find(m => m.MessageKey === messageKey);
            if (!msg) {
                await loadMessageUntilFound(messageKey, skip);
                msg = allMessages.find(m => m.MessageKey === messageKey);
            }
            if (msg) {
                if (msg.SenderKey !== memberKey) {
                    msg.Status = 2;
                    msg.Content = "Message recalled";
                    if (msg.Url) {
                        delete msg.Url;
                        delete msg.MessageType;
                        delete msg.MimeType;
                    }
                }
                messageList.innerHTML = allMessages.map(m => addMessage(m)).join("");
                const msgElement = messageList.querySelector(`[data-message-key="${messageKey}"]`);
                if (msgElement) msgElement.classList.add("recalled");
                messageList.scrollTop = messageList.scrollHeight;
                updatePinnedSection();
            }
        } else {
            console.error("[RecallResponse] Failed:", message);
            alert(message || "Recalling failed.");
        }
    });

    if (fileIcon) fileIcon.addEventListener("click", () => fileInput.click());
    if (fileInput) fileInput.addEventListener("change", (e) => {
        selectedFile = e.target.files[0];
        if (selectedFile) {
            filePreviewContainer.style.display = "block";
            fileInput.disabled = true;
            const fileType = selectedFile.type;
            if (fileType.startsWith("image/")) {
                filePreview.src = URL.createObjectURL(selectedFile);
                filePreview.classList.remove("d-none");
                videoPreview.classList.add("d-none");
                audioPreview.classList.add("d-none");
            } else if (fileType.startsWith("video/")) {
                videoPreview.src = URL.createObjectURL(selectedFile);
                videoPreview.classList.remove("d-none");
                filePreview.classList.add("d-none");
                audioPreview.classList.add("d-none");
            } else if (fileType.startsWith("audio/")) {
                audioPreview.src = URL.createObjectURL(selectedFile);
                audioPreview.classList.remove("d-none");
                filePreview.classList.add("d-none");
                videoPreview.classList.add("d-none");
            }
        }
    });
    if (clearFile) clearFile.addEventListener("click", resetFileInput);
    if (sendIcon) sendIcon.addEventListener("click", async () => {
        const messageText = chatInput.value.trim();
        if ((messageText || selectedFile) && (currentConversationKey || currentUserKey)) {
            try {
                const formData = new FormData();
                formData.append("ConversationKey", currentConversationKey || "");
                formData.append("UserKey", memberKey);
                formData.append("UserType", currentUserType || "");
                formData.append("Content", messageText);
                if (selectedFile) formData.append("File", selectedFile);

                if (!currentConversationKey && currentUserKey) {
                    const formDataInit = new FormData();
                    formDataInit.append("UserKey", currentUserKey);
                    formDataInit.append("UserType", currentUserType);
                    formDataInit.append("MemberKey", memberKey);

                    const initResponse = await fetch("/api/conversations/init", { method: "POST", body: formDataInit });
                    if (!initResponse.ok) throw new Error("[sendIcon] Failed to initialize conversation");
                    const initData = await initResponse.json();
                    currentConversationKey = initData.ConversationKey;
                    currentConversationType = initData.ConversationType || "Private";
                    updateIconsVisibility();
                }

                const response = await fetch("/api/conversations/messages", { method: "POST", body: formData });
                if (!response.ok) throw new Error("[sendIcon] Failed to send message");
                chatInput.value = "";
                resetFileInput();
                skip = 0;
                allMessages = [];
                await loadMessages(currentConversationKey, false, skip);
            } catch (err) {
                console.error("[sendIcon] Error sending message:", err);
            }
        }
    });
    if (openChat) openChat.addEventListener("click", async () => {
        const chatLoading = document.getElementById('chatLoading');
        if (chatLoading) chatLoading.classList.remove('d-none');
        try {
            await startConnection(window.connection, memberKey);
            $('#chatModal').modal('show');
            loadConversations();
            window.dispatchEvent(new CustomEvent('openGroupPopup', { detail: { memberKey } }));
        } catch (err) {
            console.error('SignalR connection failed:', err);
        } finally {
            if (chatLoading) chatLoading.classList.add('d-none');
        }
    });
    if (closeChat) closeChat.addEventListener("click", () => {
        resetChatInterface();
        $(chatModal).modal("hide");
    });
    if (chatModal) {
        $(chatModal).on('shown.bs.modal', () => {
            clearInterval(unreadInterval);
            updateIconsVisibility();
        });
        $(chatModal).on('hidden.bs.modal', () => {
            resetChatInterface();
            if (window.isAuthenticated) {
                unreadInterval = setInterval(updateUnreadCountInitial, 60000);
                updateUnreadCountInitial();
            }
        });
    }

    if (searchInput) {
        let isSearching = false;
        searchInput.addEventListener("focus", () => {
            conversationListContainer.classList.add("focused");
            searchResults.classList.add("show");
        });
        searchInput.addEventListener("blur", () => {
            conversationListContainer.classList.remove("focused");
            searchResults.classList.remove("show");
        });
        searchInput.addEventListener("input", debounce(async (e) => {
            if (isSearching) return;
            isSearching = true;
            await searchContacts(e.target.value.trim());
            isSearching = false;
        }, 500));
    }

    document.addEventListener("click", (e) => {
        const pinnedSection = document.getElementById("pinnedSection");
        const chatHeaderContent = document.getElementById("chatHeaderContent");

        if (pinnedSection && pinnedSection.style.display !== "none" && pinnedSection.contains(e.target)) {
            showPinnedPopup();
            return;
        }

        if (chatHeaderContent && chatHeaderContent.style.display !== "none" && chatHeaderContent.contains(e.target)) {
            showPinnedPopup();
        }
    });

    messageList.addEventListener('click', (e) => {
        const optionsButton = e.target.closest('.message-options');
        if (optionsButton) {
            const messageElement = optionsButton.closest('.message');
            const messageKey = messageElement.dataset.messageKey;
            const senderKey = messageElement.dataset.senderKey;
            showMessageOptions(optionsButton, messageKey, senderKey);
        }
    });

    function showMessageOptions(targetIcon, messageKey, senderKey) {
        const existingMenu = document.getElementById("messageOptionsMenu");
        if (existingMenu) existingMenu.remove();

        const isMyMessage = senderKey === memberKey;
        const message = allMessages.find(m => m.MessageKey === messageKey);
        const isPinned = message ? message.IsPinned : false;

        const menu = document.createElement("div");
        menu.id = "messageOptionsMenu";
        menu.className = "message-options-menu";
        menu.innerHTML = `
            <div class="menu-item" data-action="${isPinned ? 'unpin' : 'pin'}">${isPinned ? '📌 Unpin' : '📌 Pin'}</div>
            ${isMyMessage ? '<div class="menu-item" data-action="recall">↩️ Recall</div>' : ""}
            <div class="menu-item" data-action="reply">💬 Reply</div>
        `;

        const modalContent = document.querySelector('#chatModal .modal-content');
        if (!modalContent) return;
        modalContent.appendChild(menu);

        const iconRect = targetIcon.getBoundingClientRect();
        const modalRect = modalContent.getBoundingClientRect();
        let top = iconRect.top - modalRect.top + targetIcon.offsetHeight;
        let left = iconRect.left - modalRect.left;

        const menuHeight = menu.offsetHeight || 90;
        const menuWidth = menu.offsetWidth || 120;

        if (left + menuWidth > modalRect.width) left = modalRect.width - menuWidth - 10;
        if (top + menuHeight > modalRect.height) top = iconRect.top - modalRect.top - menuHeight;

        Object.assign(menu.style, {
            position: "absolute",
            top: `${top}px`,
            left: `${left}px`,
            zIndex: 1051,
            display: "block",
            background: "linear-gradient(90deg, #6a11cb 0%, #2575fc 100%)",
            color: "#fff",
            borderRadius: "4px",
            boxShadow: "0 2px 5px rgba(0,0,0,0.2)",
            padding: "5px 0"
        });

        menu.querySelectorAll(".menu-item").forEach(item => {
            item.style.padding = "5px 15px";
            item.addEventListener("click", (e) => {
                e.stopPropagation();
                const action = item.dataset.action;
                menu.remove();
                if (action === "pin") pinMessage(messageKey);
                if (action === "unpin") unpinMessage(messageKey);
                if (action === "recall" && isMyMessage) recallMessage(messageKey);
                if (action === "reply") replyToMessage(messageKey, messageElement);
            });
        });

        const hideMenu = (e) => {
            if (!menu.contains(e.target) && !targetIcon.contains(e.target)) {
                menu.remove();
                document.removeEventListener("click", hideMenu);
                document.removeEventListener("scroll", hideMenu, true);
            }
        };
        document.addEventListener("click", hideMenu);
        document.addEventListener("scroll", hideMenu, true);
    }

    async function pinMessage(messageKey) {
        const message = allMessages.find(m => m.MessageKey === messageKey);
        if (!message) return;

        const pinnedMessages = allMessages.filter(m => m.IsPinned);
        if (pinnedMessages.length >= 3) {
            alert("Reached the limit of pinned messages (3/3)");
            return;
        }

        let conversationKey = currentConversationKey;
        if (!conversationKey) {
            const activeConv = document.querySelector(".conversation-item.active");
            conversationKey = activeConv ? activeConv.getAttribute("data-conversation-key") : null;
            if (!conversationKey) {
                console.error("[pinMessage] No conversationKey found.");
                alert("Please select a conversation before pinning.");
                return;
            }
        }

        try {
            const response = await fetch(`/api/conversations/pin/${messageKey}?conversationKey=${conversationKey}`, {
                method: 'PUT',
                credentials: 'include'
            });
            const result = await response.json();
            if (result.success) {
                message.IsPinned = true;
                messageList.innerHTML = allMessages.map(m => addMessage(m)).join("");
                messageList.scrollTop = messageList.scrollHeight;
                updatePinnedSection();
            } else {
                alert(result.message || "Pinning failed.");
            }
        } catch (err) {
            console.error("[pinMessage] Error pinning message:", err);
            alert("Error pinning message. Check console for details.");
        }
    }

    async function unpinMessage(messageKey) {
        let conversationKey = currentConversationKey;
        if (!conversationKey) {
            const activeConv = document.querySelector(".conversation-item.active");
            conversationKey = activeConv ? activeConv.getAttribute("data-conversation-key") : null;
            if (!conversationKey) {
                console.error("[unpinMessage] No conversationKey found.");
                alert("Please select a conversation before unpinning.");
                return;
            }
        }

        try {
            const response = await fetch(`/api/conversations/unpin/${messageKey}?conversationKey=${conversationKey}`, {
                method: 'PUT',
                credentials: 'include'
            });
            const result = await response.json();
            if (result.success) {
                const message = allMessages.find(m => m.MessageKey === messageKey);
                if (message) {
                    message.IsPinned = false;
                    messageList.innerHTML = allMessages.map(m => addMessage(m)).join("");
                    messageList.scrollTop = messageList.scrollHeight;
                    updatePinnedSection();
                }
            } else {
                alert(result.message || "Unpinning failed.");
            }
        } catch (err) {
            console.error("[unpinMessage] Error unpinning message:", err);
            alert("Error unpinning message. Check console for details.");
        }
    }

    async function recallMessage(messageKey) {
        const message = allMessages.find(m => m.MessageKey === messageKey);
        if (!message || message.SenderKey !== memberKey || message.Status === 2) return;

        let conversationKey = currentConversationKey;
        if (!conversationKey) {
            const activeConv = document.querySelector(".conversation-item.active");
            conversationKey = activeConv ? activeConv.getAttribute("data-conversation-key") : null;
            if (!conversationKey) {
                console.error("[recallMessage] No conversationKey found.");
                alert("Please select a conversation before recalling.");
                return;
            }
        }

        try {
            const response = await fetch(`/api/conversations/recall/${messageKey}?conversationKey=${conversationKey}`, {
                method: 'PUT',
                credentials: 'include'
            });
            const result = await response.json();
            if (result.success) {
                message.Status = 2;
                message.Content = "Message recalled";
                if (message.Url) {
                    delete message.Url;
                    delete message.MessageType;
                    delete message.MimeType;
                }
                messageList.innerHTML = allMessages.map(m => addMessage(m)).join("");
                const msgElement = messageList.querySelector(`[data-message-key="${messageKey}"]`);
                if (msgElement) msgElement.classList.add("recalled");
                messageList.scrollTop = messageList.scrollHeight;
                updatePinnedSection();
            } else {
                alert(result.message || "Recalling failed.");
            }
        } catch (err) {
            console.error("[recallMessage] Error recalling message:", err);
            alert("Error recalling message. Check console for details.");
        }
    }

    function replyToMessage(messageKey, messageElement) {
        const message = allMessages.find(m => m.MessageKey === messageKey);
        if (message) {
            chatInput.value = `${message.Content ? `Replying to ${message.SenderName || "someone"}: ${message.Content}` : "Replying to message"}`;
            chatInput.focus();
            messageElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
    }

    const hamburgerBtn = document.querySelector('.hamburger-btn');
    const navMenu = document.querySelector('.nav-menu');
    if (hamburgerBtn && navMenu) {
        hamburgerBtn.addEventListener('click', () => navMenu.classList.toggle('show'));
    }
    const dropdownElements = Array.prototype.slice.call(document.querySelectorAll('[data-bs-toggle="dropdown"]'));
    dropdownElements.forEach(dropdownToggleEl => new bootstrap.Dropdown(dropdownToggleEl));

    window.addEventListener('beforeunload', () => {
        if (window.connection.state === signalR.HubConnectionState.Connected) {
            window.connection.stop();
        }
    });
});
//window.goupDetails_modal = null;
//window.showGroupDetails = function (conversationKey) {
//    if (!window.goupDetails_modal) {
//        window.goupDetails_modal = new bootstrap.Modal(document.getElementById('group_details_modal'), {
//            keyboard: false
//        });
//    }

//    fetch(`/api/conversations/GetGroupDetails/${conversationKey}`)
//        .then(response => response.text())
//        .then(html => {
//            document.getElementById('group_details_modal_content').innerHTML = html;
//            window.goupDetails_modal.show();
//        })
//        .catch(error => console.error('Error loading group details:', error));
//};

window.goupDetails_modal = null;
window.showGroupDetails = function (conversationKey) {
    if (!window.goupDetails_modal) {
        window.goupDetails_modal = new bootstrap.Modal(document.getElementById('group_details_modal'), {
            keyboard: false
        });
    }
    window.goupDetails_modal.show();
};