
async function startConnection(connection, memberKey) {
    try {
        console.log("Checking connection:", connection.state);
        if (connection.state !== signalR.HubConnectionState.Disconnected) {
            await connection.stop();
        }
        await connection.start();
        console.log("[startConnection] Connected to ChatHub successfully");

        // Gọi InitializeConnection để server biết connection này thuộc memberKey
        await connection.invoke("InitializeConnection", null, memberKey);

        //// Các handler hiện có
        //connection.on('ReceiveMessage', updateUnreadCount);
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
        connection.on("UpdateGroupAvatar", (conversationKey, newAvatarUrl) => {
            try {
                console.log("[UpdateGroupAvatar] received", { conversationKey, newAvatarUrl, currentConversationKey });

                if (!conversationKey) return;
                const bustUrl = (newAvatarUrl || '/images/avatar/default-avatar.jpg') + '?v=' + Date.now();

                // 1) Update conversation list (if exists)
                const listItem = document.querySelector(`.conversation-item[data-conversation-key="${conversationKey}"]`);
                if (listItem) {
                    const listAvatar = listItem.querySelector("img");
                    if (listAvatar) {
                        listAvatar.src = bustUrl;
                        console.log("[UpdateGroupAvatar] updated list avatar");
                    }
                } else {
                    console.log("[UpdateGroupAvatar] listItem not found");
                }

                // 2) Update Group Details modal if open
                const groupAvatar = document.getElementById('groupAvatar');
                if (groupAvatar) {
                    groupAvatar.src = bustUrl;
                    console.log("[UpdateGroupAvatar] updated modal avatar");
                }

                // cache
                updatedGroupAvatars[conversationKey] = bustUrl;

                // 3) Only update header when this conversation is currently open in this tab
                if (String(currentConversationKey) === String(conversationKey)) {
                    // try to set header now, if header not in DOM yet try again shortly
                    const applyHeader = () => {
                        const headerAvatarEl = document.getElementById('headerAvatar');
                        if (headerAvatarEl) {
                            headerAvatarEl.src = bustUrl;
                            console.log("[UpdateGroupAvatar] header updated to", bustUrl);
                            return true;
                        }
                        return false;
                    };

                    if (!applyHeader()) {
                        // retry once shortly in case of quick DOM replace
                        setTimeout(() => {
                            if (!applyHeader()) {
                                console.warn("[UpdateGroupAvatar] headerAvatar not found after retry");
                            }
                        }, 60);
                    }
                }
            } catch (ex) {
                console.error("[UpdateGroupAvatar] handler error:", ex);
            }
        });

        connection.on("UpdateGroupName", (conversationKey, newGroupName, memberName) => {
            try {
                console.log("[UpdateGroupName] received", { conversationKey, newGroupName, memberName, currentConversationKey });

                // 1) Update list conversation
                const listItem = document.querySelector(`.conversation-item[data-conversation-key="${conversationKey}"]`);
                if (listItem) {
                    const nameEl = listItem.querySelector('p.fw-bold') || listItem.querySelector('p') || listItem;
                    if (nameEl) {
                        nameEl.textContent = newGroupName;
                        console.log("[UpdateGroupName] updated list name");
                    }
                } else {
                    console.log("[UpdateGroupName] listItem not found");
                }

                // 2) Update Group Details modal if open
                const displayNameEl = document.getElementById('displayGroupName');
                if (displayNameEl) {
                    displayNameEl.textContent = newGroupName;
                    console.log("[UpdateGroupName] updated modal display name");
                } else {
                    const groupNameContainer = document.getElementById('groupName');
                    if (groupNameContainer) {
                        const p = groupNameContainer.querySelector('#displayGroupName');
                        if (p) p.textContent = newGroupName;
                        else groupNameContainer.textContent = newGroupName;
                        console.log("[UpdateGroupName] updated modal groupNameContainer");
                    }
                }

                // 3) Only update header if this conversation is currently open in this tab
                if (String(currentConversationKey) === String(conversationKey)) {
                    const applyHeaderName = () => {
                        const headerName = document.getElementById('headerName');
                        if (headerName) {
                            headerName.textContent = newGroupName;
                            console.log("[UpdateGroupName] header name updated to", newGroupName);
                            return true;
                        }
                        return false;
                    };

                    if (!applyHeaderName()) {
                        setTimeout(() => {
                            if (!applyHeaderName()) {
                                console.warn("[UpdateGroupName] headerName not found after retry");
                            }
                        }, 60);
                    }
                }


            } catch (ex) {
                console.error("[UpdateGroupName handler] error:", ex);
            }
        });

        connection.on("RemoveMember", (conversationKey, userName) => {
            console.log("[RemoveMember] received", { conversationKey, userName, currentConversationKey });
            if (String(currentConversationKey) === String(conversationKey)) {
                window.showGroupDetails(conversationKey); // Refresh danh sách thành viên
                console.log("[RemoveMember] refreshed group details");
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
let currentConversationKey = null;
const updatedGroupAvatars = {};

document.addEventListener("DOMContentLoaded", async () => {
    if (!window.signalR) {
        console.error("[DOMContentLoaded] SignalR not loaded!");
        return;
    }

    let unreadCount = 0;
    let selectedFile = null;
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

        // ✅ Ưu tiên dùng avatar mới nếu có (đã được cập nhật qua SignalR)
        if (updatedGroupAvatars[contact.ConversationKey]) {
            headerAvatar.src = updatedGroupAvatars[contact.ConversationKey];
        } else {
            headerAvatar.src = contact.Avatar || '/images/avatar/default-avatar.jpg';
        }

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

    //window.connection.on("ReceiveMessage", (message) => {
    //    // Normalize property names from server (case-insensitive)
    //    message = {
    //        ...message,
    //        ConversationKey: message.ConversationKey || message.conversationKey,
    //        MessageKey: message.MessageKey || message.messageKey,
    //        SenderKey: message.SenderKey || message.senderKey,
    //        SenderName: message.SenderName || message.senderName,
    //        SenderAvatar: message.SenderAvatar || message.senderAvatar,
    //        MessageType: message.MessageType || message.messageType,
    //        Content: message.Content || message.content,
    //        ParentMessageKey: message.ParentMessageKey || message.parentMessageKey,
    //        CreatedOn: message.CreatedOn || message.createdOn,
    //        Status: message.Status ?? message.status,
    //        IsPinned: message.IsPinned ?? message.isPinned,
    //        IsSystemMessage: message.IsSystemMessage ?? message.isSystemMessage,
    //        Url: message.Url || message.url
    //    };

    //    console.log("[ReceiveMessage] Received:", JSON.stringify(message));
    //    console.log("[ReceiveMessage] Current state:", {
    //        currentConversationKey,
    //        messageConversationKey: message.ConversationKey
    //    });

    //    allMessages.push(message);

    //    const addMessageToUI = (attempt = 1, maxAttempts = 5) => {
    //        if (!messageList) {
    //            console.warn(`[ReceiveMessage] messageList not found, attempt ${attempt}/${maxAttempts}`);
    //            if (attempt < maxAttempts) {
    //                setTimeout(() => addMessageToUI(attempt + 1, maxAttempts), 100);
    //            }
    //            return;
    //        }

    //        if (!currentConversationKey) {
    //            console.warn(`[ReceiveMessage] currentConversationKey not set, attempt ${attempt}/${maxAttempts}`);
    //            if (attempt < maxAttempts) {
    //                setTimeout(() => addMessageToUI(attempt + 1, maxAttempts), 100);
    //            }
    //            return;
    //        }

    //        // Case 1: Message belongs to the current open conversation
    //        if (String(message.ConversationKey) === String(currentConversationKey)) {
    //            console.log("[ReceiveMessage] Adding to UI:", message);

    //            // Nếu là system message -> render style đặc biệt
    //            if (message.IsSystemMessage) {
    //                const systemMsgHtml = `
    //                <li class="message system-message">
    //                    <div id="system-message-box">${message.Content}</div>
    //                </li>
    //            `;
    //                messageList.insertAdjacentHTML("beforeend", systemMsgHtml);
    //            } else {
    //                // Render tin nhắn thường
    //                messageList.insertAdjacentHTML("beforeend", addMessage(message));
    //            }

    //            // Cuộn xuống cuối
    //            setTimeout(() => {
    //                messageList.scrollTop = messageList.scrollHeight;
    //                console.log("[ReceiveMessage] Scrolled to bottom");
    //            }, 0);
    //        }
    //        // Case 2: Message belongs to another conversation
    //        else {
    //            console.log("[ReceiveMessage] Message for other conversation:", message.ConversationKey);
    //            const convItem = document.querySelector(`.conversation-item[data-conversation-key="${message.ConversationKey}"]`);
    //            if (convItem) {
    //                const lastMessageEl = convItem.querySelector("p.small.mb-0");
    //                const timeEl = convItem.querySelector("p.small.mb-1");
    //                lastMessageEl.textContent = message.Content || "New message";
    //                timeEl.textContent = formatTime(message.CreatedOn);
    //                const unreadBadge = convItem.querySelector(".badge");
    //                if (unreadBadge) {
    //                    unreadBadge.textContent = parseInt(unreadBadge.textContent) + 1 || 1;
    //                } else {
    //                    const newBadge = document.createElement("span");
    //                    newBadge.className = "badge bg-danger rounded-pill px-2";
    //                    newBadge.textContent = "1";
    //                    convItem.querySelector(".text-end").appendChild(newBadge);
    //                }
    //                unreadCount++;
    //                updateUnreadCount(unreadCount);
    //            }
    //        }

    //        updatePinnedSection();
    //    };

    //    addMessageToUI();
    //});

    window.connection.on("ReceiveMessage", (message) => {
        // Normalize property names from server (case-insensitive)
        message = {
            ...message,
            ConversationKey: message.ConversationKey || message.conversationKey,
            MessageKey: message.MessageKey || message.messageKey,
            SenderKey: message.SenderKey || message.senderKey,
            SenderName: message.SenderName || message.senderName,
            SenderAvatar: message.SenderAvatar || message.senderAvatar,
            MessageType: message.MessageType || message.messageType,
            Content: message.Content || message.content,
            ParentMessageKey: message.ParentMessageKey || message.parentMessageKey,
            CreatedOn: message.CreatedOn || message.createdOn,
            Status: message.Status ?? message.status,
            IsPinned: message.IsPinned ?? message.isPinned,
            IsSystemMessage: message.IsSystemMessage ?? message.isSystemMessage,
            Url: message.Url || message.url
        };

        console.log("[ReceiveMessage] Received:", JSON.stringify(message));
        console.log("[ReceiveMessage] Current state:", {
            currentConversationKey,
            messageConversationKey: message.ConversationKey
        });

        if (message.MessageKey && allMessages.some(m => m.MessageKey === message.MessageKey)) {
            console.log('[ReceiveMessage] duplicate message skipped:', message.MessageKey);
        } else {
            allMessages.push(message);
        }

        const addMessageToUI = (attempt = 1, maxAttempts = 5) => {
            if (!messageList) {
                console.warn(`[ReceiveMessage] messageList not found, attempt ${attempt}/${maxAttempts}`);
                if (attempt < maxAttempts) {
                    setTimeout(() => addMessageToUI(attempt + 1, maxAttempts), 100);
                }
                return;
            }

            if (!currentConversationKey) {
                console.warn(`[ReceiveMessage] currentConversationKey not set, attempt ${attempt}/${maxAttempts}`);
                if (attempt < maxAttempts) {
                    setTimeout(() => addMessageToUI(attempt + 1, maxAttempts), 100);
                }
                return;
            }

            // Nếu tin nhắn thuộc cuộc hội thoại đang mở
            if (String(message.ConversationKey) === String(currentConversationKey)) {
                console.log("[ReceiveMessage] Adding to UI:", message);

                // ✅ Luôn dùng addMessage() để render đúng CSS
                messageList.insertAdjacentHTML("beforeend", addMessage(message));

                // Cuộn xuống cuối
                setTimeout(() => {
                    messageList.scrollTop = messageList.scrollHeight;
                    console.log("[ReceiveMessage] Scrolled to bottom");
                }, 0);
            }
            // Nếu tin nhắn thuộc cuộc hội thoại khác
            else {
                console.log("[ReceiveMessage] Message for other conversation:", message.ConversationKey);
                const convItem = document.querySelector(`.conversation-item[data-conversation-key="${message.ConversationKey}"]`);
                if (convItem) {
                    const lastMessageEl = convItem.querySelector("p.small.mb-0");
                    const timeEl = convItem.querySelector("p.small.mb-1");
                    lastMessageEl.textContent = message.Content || "New message";
                    timeEl.textContent = formatTime(message.CreatedOn);
                    const unreadBadge = convItem.querySelector(".badge");
                    if (unreadBadge) {
                        unreadBadge.textContent = parseInt(unreadBadge.textContent) + 1 || 1;
                    } else {
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
        };

        addMessageToUI();
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

window.goupDetails_modal = null;
// --- Replace the whole showGroupDetails function with this robust version ---
window.goupDetails_modal = null;
window.goupDetails_modal = null;
window.showGroupDetails = async function (conversationKey) {
    try {
        const modalRoot = document.getElementById('group_details_modal');
        if (!modalRoot) {
            console.error('[showGroupDetails] group_details_modal not found in DOM.');
            return;
        }

        const detailsView = document.getElementById('group-details-view');
        const confirmationView = document.getElementById('remove-member-confirmation-view');
        if (detailsView && confirmationView) {
            detailsView.style.display = 'block';
            confirmationView.style.display = 'none';
        }

        if (!window.goupDetails_modal) {
            window.goupDetails_modal = new bootstrap.Modal(modalRoot, { keyboard: false });
        }

        const resp = await fetch(`/api/conversations/GetGroupDetails/${encodeURIComponent(conversationKey)}`, { credentials: 'include' });
        if (!resp.ok) throw new Error('[showGroupDetails] Failed to load group details: ' + resp.status);

        // Thay đổi #1: Lấy currentMemberKey từ API
        const result = await resp.json();
        const data = result.data || {};
        const currentKey = (result.currentMemberKey || '').toString();
        window.currentMemberKey = currentKey;

        const groupAvatar = modalRoot.querySelector('#groupAvatar');
        const groupNameContainer = modalRoot.querySelector('#groupName');
        const memberList = modalRoot.querySelector('#memberList');
        if (!groupAvatar || !groupNameContainer || !memberList) {
            console.error('[showGroupDetails] DOM elements not found.');
            return;
        }

        function escapeHtml(str) {
            if (!str && str !== '') return '';
            return String(str)
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;')
                .replace(/'/g, '&#39;');
        }

        // Ảnh nhóm
        groupAvatar.src = (data.GroupAvatar || '/images/avatar/default-avatar.jpg') + '?v=' + Date.now();

        // Danh sách thành viên
        memberList.innerHTML = (data.Members || []).map(m => {
            const userKey = (m.UserKey ?? m.MemberKey ?? '').toString();

            // Thay đổi #2: So sánh với currentKey để ẩn nút ❌
            const isSelf = userKey && userKey === currentKey;
            const btnHtml = isSelf
                ? ''
                : `
                <button class="ms-auto remove-member-icon"
                        title="Remove"
                        data-user-key="${escapeHtml(userKey)}"
                        data-user-name="${escapeHtml(m.UserName || m.Name || 'Member')}"
                        data-conversation-key="${escapeHtml(conversationKey)}">&times;</button>`;

            return `
    <div class="member-item d-flex align-items-center mb-2"
         data-user-key="${escapeHtml(userKey)}">

                    <img src="${escapeHtml(m.Avatar || '/images/avatar/default-avatar.jpg')}?v=${Date.now()}"
                         alt="${escapeHtml(m.UserName || m.Name || '')}"
                         class="member-avatar rounded-circle me-2"
                         style="width:36px;height:36px;object-fit:cover;">
                    <span class="member-name">${escapeHtml(m.UserName || m.Name || '')}</span>
                    ${btnHtml}
                </div>
            `;
        }).join('');

        // Tên nhóm (click để sửa)
        groupNameContainer.innerHTML = `
            <span id="displayGroupName" style="cursor: pointer; font-weight:600;">
                ${escapeHtml(data.GroupName || 'Unnamed Group')}
            </span>
            <input id="editGroupName" type="text" style="display:none; width:100%; margin-top:6px;" placeholder="Enter new group name" />
            <button id="saveGroupName" style="display:none; margin-top:6px;" class="btn btn-sm btn-primary">Save</button>
        `;

        // Upload avatar nhóm
        let fileInput = document.getElementById('groupAvatarInput');
        if (!fileInput) {
            fileInput = document.createElement('input');
            fileInput.type = 'file';
            fileInput.id = 'groupAvatarInput';
            fileInput.accept = 'image/*';
            fileInput.style.display = 'none';
            document.body.appendChild(fileInput);
        }
        fileInput.onchange = async function (e) {
            const file = e.target.files[0];
            if (!file) return;
            const fd = new FormData();
            fd.append('ConversationKey', conversationKey);
            fd.append('File', file);
            try {
                const r = await fetch('/api/conversations/UpdateGroupAvatar', { method: 'POST', body: fd, credentials: 'include' });
                const json = await r.json();
                if (json && json.success) {
                    const bustUrl = (json.newAvatarUrl || '') + '?v=' + Date.now();
                    groupAvatar.src = bustUrl;
                    const listAvatar = document.querySelector(`.conversation-item[data-conversation-key="${conversationKey}"] img`);
                    if (listAvatar) listAvatar.src = bustUrl;
                    const headerAvatar = document.getElementById('headerAvatar');
                    if (headerAvatar && String(window.currentConversationKey) === String(conversationKey)) headerAvatar.src = bustUrl;
                    (window.updatedGroupAvatars || (window.updatedGroupAvatars = {}))[conversationKey] = bustUrl;
                } else {
                    showNotification(json.message || 'ACCESS DENIED', 'error');
                }
            } catch (err) {
                console.error('Error updating avatar:', err);
                showNotification('Error updating avatar', 'error');
            } finally {
                fileInput.value = '';
            }
        };
        groupAvatar.onclick = () => fileInput.click();

        // Sửa tên nhóm
        const displayName = groupNameContainer.querySelector('#displayGroupName');
        const editInput = groupNameContainer.querySelector('#editGroupName');
        const saveButton = groupNameContainer.querySelector('#saveGroupName');
        if (displayName && editInput && saveButton) {
            displayName.onclick = function () {
                displayName.style.display = 'none';
                editInput.style.display = 'block';
                saveButton.style.display = 'inline-block';
                editInput.value = data.GroupName || '';
                editInput.focus();
            };

            saveButton.onclick = async function () {
                const newName = (editInput.value || '').trim();
                if (!newName) {
                    showNotification('Group name cannot be empty', 'error');
                    return;
                }
                const fd = new FormData();
                fd.append('ConversationKey', conversationKey);
                fd.append('NewGroupName', newName);
                try {
                    const r = await fetch('/api/conversations/UpdateGroupName', { method: 'POST', body: fd, credentials: 'include' });
                    const json = await r.json();
                    if (json && json.success) {
                        displayName.textContent = newName;
                        displayName.style.display = 'inline';
                        editInput.style.display = 'none';
                        saveButton.style.display = 'none';
                        showNotification('Group name updated', 'success');

                        const convItem = document.querySelector(`.conversation-item[data-conversation-key="${conversationKey}"]`);
                        if (convItem) {
                            const nameEl = convItem.querySelector('p.fw-bold') || convItem.querySelector('p');
                            if (nameEl) nameEl.textContent = newName;
                        }
                        const headerName = document.getElementById('headerName');
                        if (headerName && String(window.currentConversationKey) === String(conversationKey)) headerName.textContent = newName;
                    } else {
                        showNotification(json.message || 'Failed to update group name', 'error');
                    }
                } catch (err) {
                    console.error('Error updating group name:', err);
                    showNotification('Error updating group name', 'error');
                }
            };
        }

        // Nút tác vụ khác (nếu có)
        const leaveBtn = modalRoot.querySelector('.leave-group-btn');
        const addBtn = modalRoot.querySelector('.add-member-btn');
        if (leaveBtn) leaveBtn.onclick = () => showLeaveConfirmation(conversationKey);
        if (addBtn) addBtn.onclick = () => showAddMemberPopup(conversationKey);

        window.goupDetails_modal.show();
    } catch (err) {
        console.error('[showGroupDetails] Error:', err);
    }
};






// Hàm hiển thị thông báo đẹp mắt
function showNotification(message, type) {
    // Xóa thông báo cũ nếu có
    const old = document.querySelector('.notification');
    if (old) old.remove();

    // Tạo thẻ container riêng biệt nằm cuối cùng của body
    const notification = document.createElement('div');
    notification.className = `notification ${type}`;
    notification.textContent = message;

    // Style siêu cấp
    Object.assign(notification.style, {
        position: 'fixed',
        top: '20px',
        right: '20px',
        padding: '10px 20px',
        borderRadius: '5px',
        color: 'white',
        fontSize: '14px',
        backgroundColor: type === 'error' ? '#dc3545' : '#28a745',
        zIndex: '2147483647', // MAX z-index trong browser
        boxShadow: '0 2px 10px rgba(0,0,0,0.2)',
        opacity: '0',
        transition: 'opacity 0.3s ease-in-out',
        pointerEvents: 'none'
    });

    // Đảm bảo thêm vào cuối body
    setTimeout(() => {
        document.body.appendChild(notification);
        notification.style.opacity = '1';
    }, 0);

    // Tự động biến mất sau 2 giây
    setTimeout(() => {
        notification.style.opacity = '0';
        setTimeout(() => notification.remove(), 300);
    }, 2000);
}





// Hàm showLeaveConfirmation
function showLeaveConfirmation(conversationKey) {
    const modalContent = document.getElementById('group_details_modal_content');
    modalContent.innerHTML = document.getElementById('leaveConfirmationPopup').innerHTML;
    const confirmLeave = document.querySelector('.confirm-leave');
    const cancelLeave = document.querySelector('.cancel-leave');
    if (confirmLeave) {
        confirmLeave.addEventListener('click', function () {
            console.log(`Leave group with conversationKey: ${conversationKey}`);
            window.goupDetails_modal.hide();
        });
    }
    if (cancelLeave) {
        cancelLeave.addEventListener('click', function () {
            window.showGroupDetails(conversationKey);
        });
    }
}


document.addEventListener('click', function (e) {
    const btn = e.target.closest('.remove-member-icon');
    if (!btn) return;
    const conversationKey = btn.getAttribute('data-conversation-key');
    const userKey = btn.getAttribute('data-user-key');
    const userName = btn.getAttribute('data-user-name') || 'this member';
    showRemoveMemberConfirmation(conversationKey, userKey, userName);
});

function showRemoveMemberConfirmation(conversationKey, userKey, userName) {
    const detailsView = document.getElementById('group-details-view');
    const confirmationView = document.getElementById('remove-member-confirmation-view');
    const confirmationTemplate = document.getElementById('removeMemberPopup');

    if (!detailsView || !confirmationView || !confirmationTemplate) {
        console.error('[showRemoveMemberConfirmation] Required view elements not found.');
        return;
    }

    confirmationView.className = 'confirmation-content';
    confirmationView.innerHTML = confirmationTemplate.innerHTML;

    const msg = confirmationView.querySelector('#removeMemberMessage');
    if (msg) {
        msg.textContent = `Are you sure you want to remove "${userName}" from this group?`;
    } else {
        console.error('[showRemoveMemberConfirmation] #removeMemberMessage not found');
    }

    const confirmBtn = confirmationView.querySelector('.confirm-remove');
    const cancelBtn = confirmationView.querySelector('.cancel-remove');

    if (confirmBtn) {
        confirmBtn.onclick = async function () {
            await removeMemberFromGroup(conversationKey, userKey, userName);
        };
    } else {
        console.error('[showRemoveMemberConfirmation] .confirm-remove not found');
    }

    if (cancelBtn) {
        cancelBtn.onclick = function () {
            confirmationView.style.display = 'none';
            detailsView.style.display = 'block';
        };
    } else {
        console.error('[showRemoveMemberConfirmation] .cancel-remove not found');
    }

    detailsView.style.display = 'none';
    confirmationView.style.display = 'block';
}
// Hàm xóa thành viên khỏi nhóm
async function removeMemberFromGroup(conversationKey, userKey, userName) {
    const detailsView = document.getElementById('group-details-view');
    const confirmationView = document.getElementById('remove-member-confirmation-view');

    try {
        const res = await fetch(`/api/conversations/RemoveMember`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include',
            body: JSON.stringify({ conversationKey, targetUserKey: userKey, targetUserName: userName })
        });
        const data = await res.json();
        if (data && data.success) {
            showNotification('Member removed successfully', 'success');
            await window.showGroupDetails(conversationKey);
        } else {
            showNotification(data.message || 'No permission to remove', 'error');
            if (detailsView && confirmationView) {
                confirmationView.style.display = 'none';
                detailsView.style.display = 'block';
            }
        }
    } catch (err) {
        console.error('removeMemberFromGroup error:', err);
        showNotification('Error removing member', 'error');
        if (detailsView && confirmationView) {
            confirmationView.style.display = 'none';
            detailsView.style.display = 'block';
        }
    }
}

// --- Add Member UI ---

// (1) Style nho nhỏ cho list add member (chỉ chèn 1 lần)
(function ensureAddMemberStyles() {
    if (document.getElementById('add-members-styles')) return;
    const css = `
    .add-members-wrap { padding: 6px 2px; }
    .add-members-title { font-weight: 600; font-size: 16px; margin-bottom: 10px; }
    .add-members-list { 
        max-height: 360px; 
        overflow: auto; 
        padding-right: 4px; 
    }
    /* Thanh cuộn nhỏ và đồng bộ màu */
    .add-members-list::-webkit-scrollbar {
        width: 6px;
    }
    .add-members-list::-webkit-scrollbar-track {
        background: rgba(255,255,255,0.05);
    }
    .add-members-list::-webkit-scrollbar-thumb {
        background: rgba(255,255,255,0.3);
        border-radius: 3px;
    }

    .add-item { 
        display:flex; 
        align-items:center; 
        gap:10px; 
        padding:8px 10px; 
        border-radius:12px;
        background: rgba(255,255,255,0.05); 
        margin-bottom:8px; 
        cursor:pointer;
        transition: background .15s ease, transform .02s ease; 
    }
    .add-item:hover { background: rgba(255,255,255,0.08); }
    .add-item.selected { outline:2px solid rgba(13,110,253,.5); background: rgba(13,110,253,.08); }
    .add-item img { width:36px; height:36px; object-fit:cover; border-radius:50%; flex-shrink:0; }
    .add-item .name { font-weight:500; }
    
    /* Nhãn user type kiểu thẻ cào */
    .type-badge { 
        font-size: 11px; 
        text-transform: uppercase;
        padding: 2px 6px;
        border-radius: 6px;
        background: repeating-linear-gradient(
            45deg,
            rgba(255,255,255,0.15),
            rgba(255,255,255,0.15) 4px,
            transparent 4px,
            transparent 8px
        );
        color: rgba(255,255,255,0.85);
        flex-shrink: 0;
    }

    /* Checkbox nằm cuối cùng */
    .add-item .form-check-input { 
        margin-left: auto; 
        flex-shrink: 0;
    }

    .add-members-actions { 
        display:flex; 
        justify-content:flex-end; 
        gap:10px; 
        margin-top:12px; 
    }
    `;
    const style = document.createElement('style');
    style.id = 'add-members-styles';
    style.textContent = css;
    document.head.appendChild(style);
})();


// (2) Helper escape
function __escHtml(s) {
    if (!s && s !== '') return '';
    return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

// (3) Render giao diện chọn người để add
// (3) Render giao diện chọn người để add
// Thay thế nguyên hàm hiện tại bằng bản này
window.showAddMemberPopup = async function (conversationKey, preselectedKeys = []) {
    const detailsView = document.getElementById('group-details-view');
    const hostView = document.getElementById('remove-member-confirmation-view');
    if (!detailsView || !hostView) return;

    // 1) Lấy list key thành viên hiện tại (có thể remove)
    const excludeKeys = Array.from(document.querySelectorAll('#memberList .remove-member-icon'))
        .map(b => (b.getAttribute('data-user-key') || '').toString())
        .filter(Boolean);

    // 2) Loại bỏ luôn chính user đang đăng nhập (đã có sẵn currentMemberKey từ showGroupDetails)
    if (window.currentMemberKey && !excludeKeys.includes(window.currentMemberKey)) {
        excludeKeys.push(window.currentMemberKey);
    }

    let items = [];
    try {
        const res = await fetch('/api/conversations/GetAddableMembers', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include',
            body: JSON.stringify({ conversationKey, excludeKeys })
        });
        const json = await res.json();
        if (json && json.success && Array.isArray(json.items)) items = json.items;
    } catch (e) {
        console.error('[showAddMemberPopup] fetch addable error:', e);
    }

    // 3) Render UI
    hostView.className = 'confirmation-content';
    hostView.innerHTML = `
        <div class="add-members-wrap">
            <div class="add-members-title">Add members</div>
            <div id="addMembersList" class="add-members-list">
                ${items.length === 0
            ? `<div class="text-muted">No candidates found.</div>`
            : items.map(u => {
                const uk = (u.UserKey ?? '').toString();
                const checked = preselectedKeys.includes(uk);
                return `
                            <div class="add-item ${checked ? 'selected' : ''}" 
                                 data-user-key="${__escHtml(uk)}"
                                 data-user-name="${__escHtml(u.Name || '')}"
                                 data-user-type="${__escHtml(u.UserType || '')}" tabindex="0">
                                <img src="${__escHtml(u.Avatar || '/images/avatar/default-avatar.jpg')}" alt="">
                                <div class="flex-grow-1 d-flex align-items-center gap-2">
                                    <div class="name">${__escHtml(u.Name || '')}</div>
                                    <div class="type-badge">${__escHtml(u.UserType || '')}</div>
                                </div>
                                <!-- Giữ stopPropagation để không bắn lên .add-item -->
                                <input class="form-check-input" type="checkbox" ${checked ? 'checked' : ''} onclick="event.stopPropagation();" />
                            </div>`;
            }).join('')}
            </div>
            <div class="add-members-actions">
                <button id="btnCancelAdd" class="btn btn-secondary">Cancel</button>
                <button id="btnConfirmAdd" class="btn btn-primary" ${preselectedKeys.length ? '' : 'disabled'}>Add</button>
            </div>
        </div>
    `;

    // 4) Gán event
    const listEl = hostView.querySelector('#addMembersList');
    const btnAdd = hostView.querySelector('#btnConfirmAdd');
    const btnCancel = hostView.querySelector('#btnCancelAdd');

    const getSelectedKeys = () =>
        Array.from(listEl.querySelectorAll('.add-item.selected'))
            .map(it => it.getAttribute('data-user-key'))
            .filter(Boolean);

    // (A) Click vào item -> toggle cả highlight + checkbox
    listEl.addEventListener('click', (e) => {
        const item = e.target.closest('.add-item');
        if (!item) return;
        const cb = item.querySelector('input[type="checkbox"]');
        const willSelect = !item.classList.contains('selected');
        item.classList.toggle('selected', willSelect);
        if (cb) cb.checked = willSelect;
        btnAdd.disabled = getSelectedKeys().length === 0;
    });

    // (B) Bấm trực tiếp vào checkbox -> đồng bộ lại highlight
    listEl.addEventListener('change', (e) => {
        const cb = e.target;
        if (!(cb instanceof HTMLInputElement) || cb.type !== 'checkbox') return;
        const item = cb.closest('.add-item');
        if (!item) return;
        item.classList.toggle('selected', cb.checked);
        btnAdd.disabled = getSelectedKeys().length === 0;
    });

    // Cancel → quay lại GroupDetails
    btnCancel.addEventListener('click', () => {
        hostView.style.display = 'none';
        detailsView.style.display = 'block';
    });

    // Confirm → sang màn hình xác nhận
    btnAdd.addEventListener('click', () => {
        const selected = Array.from(listEl.querySelectorAll('.add-item.selected')).map(it => ({
            UserKey: it.getAttribute('data-user-key'),
            UserType: it.getAttribute('data-user-type'),
            Name: it.getAttribute('data-user-name')
        }));
        showAddMembersConfirmation(conversationKey, selected);
    });

    detailsView.style.display = 'none';
    hostView.style.display = 'block';
};



// (4) Màn hình xác nhận Add
//function showAddMembersConfirmation(conversationKey, selected) {
//    const detailsView = document.getElementById('group-details-view');
//    const hostView = document.getElementById('remove-member-confirmation-view');
//    if (!detailsView || !hostView) return;

//    const names = selected.map(s => s.Name).join(', ');
//    hostView.className = 'confirmation-content';
//    hostView.innerHTML = `
//        <div class="p-2">
//            <p style="font-weight:600;">Add the following member(s) to this group?</p>
//            <div class="mb-3 small text-break">${__escHtml(names || 'No one selected')}</div>
//            <div class="d-flex justify-content-end gap-2">
//                <button class="btn btn-secondary" id="btnBackToSelect">No</button>
//                <button class="btn btn-primary" id="btnDoAdd">Yes</button>
//            </div>
//        </div>
//    `;

//    hostView.querySelector('#btnBackToSelect').onclick = () => {
//        const preselected = selected.map(s => s.UserKey);
//        window.showAddMemberPopup(conversationKey, preselected);
//    };

//    hostView.querySelector('#btnDoAdd').onclick = async () => {
//        try {
//            const res = await fetch('/api/conversations/AddMembers', {
//                method: 'POST',
//                headers: { 'Content-Type': 'application/json' },
//                credentials: 'include',
//                body: JSON.stringify({
//                    conversationKey,
//                    newMembers: selected.map(s => ({
//                        userKey: s.UserKey,
//                        userType: s.UserType,
//                        userName: s.Name
//                    }))
//                })
//            });
//            const json = await res.json();
//            if (json && json.success) {
//                showNotification('Members added successfully', 'success');
//                await window.showGroupDetails(conversationKey);
//            } else {
//                showNotification(json.message || 'Failed to add members', 'error');
//                window.showAddMemberPopup(conversationKey, selected.map(s => s.UserKey));
//            }
//        } catch (err) {
//            console.error('[AddMembers] error:', err);
//            showNotification('Error adding members', 'error');
//            window.showAddMemberPopup(conversationKey, selected.map(s => s.UserKey));
//        }
//    };

//    detailsView.style.display = 'none';
//    hostView.style.display = 'block';
//}
function showAddMembersConfirmation(conversationKey, selected) {
    window.allMessages = window.allMessages || []; // đảm bảo tồn tại

    const detailsView = document.getElementById('group-details-view');
    const hostView = document.getElementById('remove-member-confirmation-view');
    if (!detailsView || !hostView) return;

    const names = selected.map(s => s.Name).join(', ');
    hostView.className = 'confirmation-content';
    hostView.innerHTML = `
        <div class="p-2">
            <p style="font-weight:600;">Add the following member(s) to this group?</p>
            <div class="mb-3 small text-break">${__escHtml(names || 'No one selected')}</div>
            <div class="d-flex justify-content-end gap-2">
                <button class="btn btn-secondary" id="btnBackToSelect">No</button>
                <button class="btn btn-primary" id="btnDoAdd">Yes</button>
            </div>
        </div>
    `;

    hostView.querySelector('#btnBackToSelect').onclick = () => {
        const preselected = selected.map(s => s.UserKey);
        window.showAddMemberPopup(conversationKey, preselected);
    };

    hostView.querySelector('#btnDoAdd').onclick = async () => {
        const payload = {
            conversationKey,
            newMembers: selected.map(s => ({
                userKey: s.UserKey,
                userType: s.UserType,
                userName: s.Name
            }))
        };

        try {
            const res = await fetch('/api/conversations/AddMembers', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify(payload)
            });
            const json = await res.json();
            console.log('[AddMembers] raw json:', json);

            const isSuccess = json && (json.success === true || String(json.success).toLowerCase() === 'true');
            if (!isSuccess) {
                showNotification(json?.message || 'Failed to add members', 'error');
                window.showAddMemberPopup(conversationKey, selected.map(s => s.UserKey));
                return;
            }

            const result = json.data || json;
            const msgs = Array.isArray(result?.messages) ? result.messages : [];

            if (msgs.length > 0) {
                const normalized = msgs.map(m => ({
                    MessageKey: (m["messageKey"] || m["MessageKey"] || '').toString(),
                    ConversationKey: conversationKey,
                    SenderKey: null,
                    SenderName: null,
                    SenderAvatar: null,
                    MessageType: "Text",
                    Content: m["systemContent"] || m["content"] || m["Content"] || '',
                    ParentMessageKey: null,
                    CreatedOn: m["createdOn"] || new Date().toISOString(),
                    Status: 1,
                    IsPinned: false,
                    IsSystemMessage: true,
                    Url: null
                }));

                normalized.forEach(msg => {
                    if (!msg.MessageKey) return;
                    if (!window.allMessages.some(x => x.MessageKey === msg.MessageKey)) {
                        window.allMessages.push(msg);
                    } else {
                        console.log('[AddMembersConfirmation] skipped existing message', msg.MessageKey);
                    }
                });

                if (String(window.currentConversationKey) === String(conversationKey)) {
                    const msgsForConv = window.allMessages
                        .filter(m => String(m.ConversationKey) === String(window.currentConversationKey))
                        .sort((a, b) => new Date(a.CreatedOn) - new Date(b.CreatedOn));
                    messageList.innerHTML = msgsForConv.map(m => addMessage(m)).join('');
                    setTimeout(() => {
                        messageList.scrollTop = messageList.scrollHeight;
                    }, 0);
                } else {
                    const convItem = document.querySelector(`.conversation-item[data-conversation-key="${conversationKey}"]`);
                    if (convItem) {
                        const lastMessageEl = convItem.querySelector("p.small.mb-0");
                        const timeEl = convItem.querySelector("p.small.mb-1");
                        if (lastMessageEl) lastMessageEl.textContent = normalized[normalized.length - 1].Content || 'New message';
                        if (timeEl) {
                            const formatFn = window.formatTime || function (dt) {
                                try {
                                    return new Date(dt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
                                } catch {
                                    return '';
                                }
                            };
                            timeEl.textContent = formatFn(normalized[normalized.length - 1].CreatedOn);
                        }


                        const unreadBadge = convItem.querySelector(".badge");
                        if (unreadBadge) {
                            unreadBadge.textContent = (parseInt(unreadBadge.textContent) || 0) + normalized.length;
                        } else {
                            const newBadge = document.createElement("span");
                            newBadge.className = "badge bg-danger rounded-pill px-2";
                            newBadge.textContent = String(normalized.length);
                            const endContainer = convItem.querySelector(".text-end") || convItem;
                            endContainer.appendChild(newBadge);
                        }

                        window.unreadCount = (typeof window.unreadCount === 'number')
                            ? window.unreadCount + normalized.length
                            : normalized.length;
                        if (typeof updateUnreadCount === 'function') updateUnreadCount(window.unreadCount);
                    }
                }
            }

            showNotification('Members added successfully', 'success');
            await window.showGroupDetails(conversationKey);

        } catch (err) {
            console.error('[AddMembers] error:', err);
            showNotification('Error adding members', 'error');
            window.showAddMemberPopup(conversationKey, selected.map(s => s.UserKey));
        }
    };

    detailsView.style.display = 'none';
    hostView.style.display = 'block';
}


