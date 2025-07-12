document.addEventListener("DOMContentLoaded", async () => {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("https://localhost:7003/chatHub")
        .withAutomaticReconnect()
        .build();

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
        else console.warn("Failed to fetch MemberKey:", await response.text());
    } catch (error) {
        console.error("Error fetching MemberKey:", error);
    }

    if (!memberKey) {
        console.warn("MemberKey not found. Chat disabled.");
        document.getElementById("openChat")?.addEventListener("click", () => alert("Please log in."));
        return;
    }

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

    function debounce(func, wait) {
        let timeout;
        return (...args) => {
            clearTimeout(timeout);
            timeout = setTimeout(() => func.apply(this, args), wait);
        };
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

    function formatTime(createdOn) {
        const now = new Date();
        const diffMs = now - new Date(createdOn);
        const diffMins = Math.floor(diffMs / 60000);
        const diffHours = Math.floor(diffMs / 3600000);
        const diffDays = Math.floor(diffMs / 86400000);

        return diffDays === 0 ? (diffMins < 60 ? `${diffMins} phút trước` : `${diffHours} giờ trước`) :
            new Date(createdOn).toLocaleString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" });
    }

    async function loadConversations() {
        try {
            const response = await fetch(`/api/conversations?memberKey=${encodeURIComponent(memberKey)}`);
            if (!response.ok) throw new Error(`API failed: ${await response.text()} (Status: ${response.status})`);
            const { conversations } = await response.json();
            conversationList.innerHTML = "";
            conversations.forEach(conv => {
                const li = document.createElement("li");
                li.className = "p-2 border-bottom border-white border-opacity-25";
                li.innerHTML = `
                    <a href="#" class="d-flex justify-content-between text-white conversation-item" 
                        data-conversation-key="${conv.ConversationKey}" 
                        data-user-key="${conv.ConversationType !== 'Group' ? (conv.PartnerUserKey || '') : ''}" 
                        data-user-type="${conv.ConversationType !== 'Group' ? (conv.PartnerUserType || '') : ''}"
                        data-conversation-type="${conv.ConversationType || ''}">
                        <div class="d-flex">
                            <img src="${conv.Avatar || '/images/avatar/default-avatar.jpg'}" alt="avatar" class="rounded-circle me-3" style="width: 48px; height: 48px;">
                            <div><p class="fw-bold mb-0">${conv.DisplayName || "Unknown"}</p><p class="small mb-0">${conv.LastMessage || "No messages"}</p></div>
                        </div>
                        <div class="text-end"><p class="small mb-1">${conv.LastMessageTime ? formatTime(conv.LastMessageTime) : ""}</p>${conv.UnreadCount > 0 ? `<span class="badge bg-danger rounded-pill px-2">${conv.UnreadCount}</span>` : ''}</div>
                    </a>
                `;
                conversationList.appendChild(li);
            });
            addConversationClickListeners();
        } catch (err) {
            console.error("Load conversations failed:", err);
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
            if (!response.ok) throw new Error(`Search failed: ${await response.text()} (Status: ${response.status})`);
            const results = await response.json();
            searchResults.innerHTML = results.length === 0 ? `<div class="no-results">Not Found</div>` : results.map(result => `
                <div class="search-result-item" data-contact='${JSON.stringify(result).replace(/'/g, "\\'")}' onmousedown="event.preventDefault()">
                    <img src="${result.Avatar || '/images/avatar/default-avatar.jpg'}" alt="${result.Name}" class="rounded-circle">
                    <p>${result.Name}</p>
                </div>
            `).join("");
            searchResults.classList.add("show");
            document.querySelectorAll(".search-result-item").forEach(item => {
                item.addEventListener("click", () => {
                    const contact = JSON.parse(item.getAttribute("data-contact"));
                    selectContact(contact);
                });
            });
        } catch (err) {
            console.error("Search error:", err);
            searchResults.innerHTML = `<div class="no-results">Not Found</div>`;
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
        pinnedSection.style.display = "none";
        skip = 0;
        allMessages = [];
        if (currentConversationKey) loadMessages(currentConversationKey);
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
                pinnedSection.style.display = "block";
                skip = 0;
                allMessages = [];
                loadMessages(currentConversationKey);
            });
        });
    }

    async function loadMessages(conversationKey, append = false) {
        if (!conversationKey) return;
        const url = `/api/conversations/messages/${conversationKey}?skip=${skip}&memberKey=${encodeURIComponent(memberKey)}`;
        try {
            const response = await fetch(url);
            if (!response.ok) throw new Error(`Load messages failed: ${await response.text()}`);
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
                messageList.scrollTop = messageList.scrollHeight;
            }

            skip += newMessages.length;

            const pinnedMessages = allMessages.filter(m => m.IsPinned);
            const firstPinned = pinnedMessages[0];
            const headerText = firstPinned ? (firstPinned.Content || `Pinned ${firstPinned.MessageType}`) : "No pinned messages";
            pinnedSection.innerHTML = `<p>${headerText} (${pinnedMessages.length}/3 pinned)</p>`;
            pinnedSection.style.display = pinnedMessages.length > 0 ? "block" : "none";
        } catch (err) {
            console.error("Load messages failed:", err);
        }
    }

    async function loadMessageUntilFound(messageKey) {
        let currentSkip = skip;
        while (true) {
            const url = `/api/conversations/messages/${currentConversationKey}?skip=${currentSkip}&memberKey=${encodeURIComponent(memberKey)}`;
            const response = await fetch(url);
            if (!response.ok) throw new Error(`Load messages failed: ${await response.text()}`);
            const newMessages = await response.json();
            if (newMessages.length === 0) break;
            newMessages.reverse();
            allMessages = [...newMessages, ...allMessages];
            messageList.innerHTML = allMessages.map(m => addMessage(m)).join("");
            const foundMessage = allMessages.find(m => m.MessageKey === messageKey);
            if (foundMessage) {
                const messageElement = document.querySelector(`[data-message-key="${messageKey}"]`);
                if (messageElement) {
                    messageElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
                }
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

        pinnedPopup.addEventListener("click", (e) => {
            if (e.target.classList.contains("pinned-unpin-btn")) {
                const messageKey = e.target.getAttribute("data-message-key");
                allMessages = allMessages.map(m => m.MessageKey === messageKey ? { ...m, IsPinned: false } : m);

                const newPinnedMessages = allMessages.filter(m => m.IsPinned);
                let newHeaderText = "No pinned messages";
                if (newPinnedMessages.length > 0) {
                    const firstNewPinned = newPinnedMessages[0];
                    newHeaderText = firstNewPinned.Content || `Pinned ${firstNewPinned.MessageType || 'Item'}`;
                }
                pinnedSection.innerHTML = `<p>${newHeaderText} (${newPinnedMessages.length}/3 pinned)</p>`;

                pinnedSection.style.display = allMessages.some(m => m.IsPinned) ? "block" : "none";
                showPinnedPopup();
            } else if (e.target.classList.contains("content")) {
                const messageKey = e.target.getAttribute("data-message-key");
                pinnedPopup.style.display = "none";
                const existingMessage = allMessages.find(m => m.MessageKey === messageKey);
                if (existingMessage) {
                    const messageElement = document.querySelector(`[data-message-key="${messageKey}"]`);
                    if (messageElement) {
                        messageElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
                    }
                } else {
                    loadMessageUntilFound(messageKey);
                }
            }
        });
    }

    function addMessage(message) {
        const isOwn = message.SenderKey === memberKey;
        const isRecalled = message.Status === 2;
        const senderName = isOwn ? "You" : (message.SenderName || "Unknown");
        const senderAvatar = isOwn ? "" : (message.SenderAvatar ? `<img src="${message.SenderAvatar || '/images/avatar/default-avatar.jpg'}" class="avatar">` : `<img src="/images/avatar/default-avatar.jpg" class="avatar">`);
        const time = formatTime(message.CreatedOn);
        const status = isOwn ? (message.Status === 0 ? '✔' : '✔✔') : '';

        let html = `
            <li class="message ${isOwn ? 'right' : 'left'} ${message.MessageType ? 'with-attachment' : ''} ${isRecalled ? 'recalled' : ''}" data-message-key="${message.MessageKey}">
                ${senderAvatar}
        `;

        if (!isRecalled) {
            html += `
                <div class="message-box">
                    <div class="message-options"><i class="fas fa-ellipsis-h"></i></div>
                    ${!isOwn && currentConversationType === 'Group' ? `<p class="name">${senderName}</p><hr>` : ''}
                    <p class="content">${allMessages.find(c => c.ConversationKey === currentConversationKey)?.IsBanned ? 'Đã bị chặn' : message.Content}</p>
                </div>
            `;
            if (message.MessageType && (message.MessageType === "Image" || message.MessageType === "Audio" || message.MessageType === "Video")) {
                html += `
                    <div class="attachment">
                        ${message.MessageType === "Image" ? `<img src="${message.Url}" class="attachment-media" alt="${message.FileName}">` :
                        message.MessageType === "Audio" ? `<audio controls><source src="${message.Url}" type="${message.MimeType}"></audio>` :
                            `<video controls><source src="${message.Url}" type="${message.MimeType}"></video>`}
                    </div>
                `;
            }
        } else {
            html += `
                <div class="message-box recalled">
                    <p class="content">Message recalled</p>
                </div>
            `;
        }

        html += `
                <div class="message-timestamp">
                    <span class="time">${time}</span>
                    ${isOwn ? `<span class="status">${status}</span>` : ''}
                </div>
            </li>
        `;
        return html;
    }

    messageList.addEventListener("scroll", debounce(() => {
        if (messageList.scrollTop === 0 && currentConversationKey) {
            loadMessages(currentConversationKey, true);
        }
    }, 300));

    async function startConnection() {
        try {
            await connection.start();
            console.log("Connected to ChatHub");
            loadConversations();
        } catch (err) {
            console.error("Connection failed:", err);
            setTimeout(startConnection, 5000);
        }
    }

    startConnection();

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

                    const initResponse = await fetch("/api/conversations/init", {
                        method: "POST",
                        body: formDataInit
                    });
                    if (!initResponse.ok) throw new Error("Init conversation failed");
                    const initData = await initResponse.json();
                    currentConversationKey = initData.ConversationKey;
                    currentConversationType = initData.ConversationType || "Private";
                }

                const response = await fetch("/api/conversations/messages", {
                    method: "POST",
                    body: formData
                });
                if (!response.ok) throw new Error("Send failed");
                chatInput.value = "";
                resetFileInput();
                skip = 0;
                allMessages = [];
                await loadMessages(currentConversationKey);
            } catch (err) {
                console.error("Send message failed:", err);
            }
        }
    });
    if (openChat) openChat.addEventListener("click", () => {
        if (chatModal) {
            $(chatModal).modal("show");
            unreadCount = 0;
            updateUnreadCount(unreadCount);
            loadConversations();
        }
    });
    if (closeChat) closeChat.addEventListener("click", () => chatModal && $(chatModal).modal("hide"));

    connection.on("ReceiveMessage", (message) => {
        if ((currentConversationKey && message.ConversationKey === currentConversationKey) || (currentUserKey && message.SenderKey === currentUserKey)) {
            allMessages.push(message);
            messageList.insertAdjacentHTML("beforeend", addMessage(message));
            messageList.scrollTop = messageList.scrollHeight;
            unreadCount++;
            updateUnreadCount(unreadCount);

            const pinnedMessages = allMessages.filter(m => m.IsPinned);
            const firstPinned = pinnedMessages[0];
            const headerText = firstPinned ? (firstPinned.Content || `Pinned ${firstPinned.MessageType}`) : "No pinned messages";
            pinnedSection.innerHTML = `<p>${headerText} (${pinnedMessages.length}/3 pinned)</p>`;
            pinnedSection.style.display = pinnedMessages.length > 0 ? "block" : "none";
        }
    });

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

    function updateUnreadCount(count) {
        const badge = document.getElementById("unreadCount");
        if (badge) {
            badge.textContent = count;
            badge.classList.toggle("d-none", count === 0);
        }
    }

    if (chatHeaderContent) {
        chatHeaderContent.addEventListener("click", showPinnedPopup);
    }

    if (pinnedSection) pinnedSection.addEventListener("click", showPinnedPopup);

    messageList.addEventListener('click', (e) => {
        const optionsButton = e.target.closest('.message-options');
        if (optionsButton) {
            const messageElement = optionsButton.closest('.message');
            const messageKey = messageElement.dataset.messageKey;
            handleMessageOptionsClick(messageKey, optionsButton);
        }
    });

    function handleMessageOptionsClick(messageKey, optionsButtonElement) {
        const existingMenu = document.querySelector('.message-context-menu');
        if (existingMenu) existingMenu.remove();

        const menu = document.createElement('div');
        menu.className = 'message-context-menu';
        menu.innerHTML = `
            <div data-action="pin">Pin</div>
            <div data-action="recall">Recall</div>
        `;
        document.body.appendChild(menu);

        const rect = optionsButtonElement.getBoundingClientRect();
        menu.style.top = `${rect.bottom + window.scrollY}px`;
        menu.style.left = `${rect.left + window.scrollX}px`;

        menu.addEventListener('click', (e) => {
            if (e.target.dataset.action) {
                console.log(`Action ${e.target.dataset.action} for message ${messageKey}`);
                menu.remove();
                if (e.target.dataset.action === 'pin') pinMessage(messageKey);
                if (e.target.dataset.action === 'recall') recallMessage(messageKey);
            }
        });

        document.addEventListener('click', function hideMenu(e) {
            if (!menu.contains(e.target)) {
                menu.remove();
                document.removeEventListener('click', hideMenu);
            }
        });
    }

    function pinMessage(messageKey) {
        const message = allMessages.find(m => m.MessageKey === messageKey);
        if (message && !message.IsPinned && allMessages.filter(m => m.IsPinned).length < 3) {
            message.IsPinned = true;
            messageList.innerHTML = allMessages.map(m => addMessage(m)).join("");
            messageList.scrollTop = messageList.scrollHeight;
            const pinnedMessages = allMessages.filter(m => m.IsPinned);
            const firstPinned = pinnedMessages[0];
            const headerText = firstPinned ? (firstPinned.Content || `Pinned ${firstPinned.MessageType}`) : "No pinned messages";
            pinnedSection.innerHTML = `<p>${headerText} (${pinnedMessages.length}/3 pinned)</p>`;
            pinnedSection.style.display = "block";
        }
    }

    function recallMessage(messageKey) {
        const message = allMessages.find(m => m.MessageKey === messageKey);
        if (message && message.SenderKey === memberKey && !message.IsRecalled) {
            message.Status = 2;
            messageList.innerHTML = allMessages.map(m => addMessage(m)).join("");
            messageList.scrollTop = messageList.scrollHeight;
        }
    }
});