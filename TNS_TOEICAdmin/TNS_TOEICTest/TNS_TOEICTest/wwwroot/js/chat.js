document.addEventListener("DOMContentLoaded", () => {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("https://localhost:7003/chatHub")
        .withAutomaticReconnect()
        .build();

    let unreadCount = 0;
    let selectedFile = null;
    let currentConversationKey = null;
    let currentUserKey = null;
    let currentUserType = null;

    const openChat = document.getElementById("openChat");
    const closeChat = document.getElementById("closeChat");
    const chatModal = document.getElementById("chatModal");
    const unreadCountBadge = document.getElementById("unreadCount");
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
    const chatHeader = document.getElementById("chatHeader");
    const headerAvatar = document.getElementById("headerAvatar");
    const headerName = document.getElementById("headerName");

    function debounce(func, wait) {
        let timeout;
        return function (...args) {
            clearTimeout(timeout);
            timeout = setTimeout(() => func.apply(this, args), wait);
        };
    }

    function updateUnreadCount(count) {
        if (unreadCountBadge) {
            unreadCountBadge.textContent = count;
            unreadCountBadge.classList.toggle("d-none", count === 0);
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

    function formatTime(createdOn) {
        const now = new Date();
        const diffMs = now - new Date(createdOn);
        const diffMins = Math.floor(diffMs / 60000);
        const diffHours = Math.floor(diffMs / 3600000);
        const diffDays = Math.floor(diffMs / 86400000);

        if (diffDays === 0) {
            if (diffMins < 60) return `${diffMins} phút trước`;
            return `${diffHours} giờ trước`;
        }
        return new Date(createdOn).toLocaleString("vi-VN", {
            day: "2-digit",
            month: "2-digit",
            year: "numeric",
            hour: "2-digit",
            minute: "2-digit"
        });
    }

    async function loadConversations() {
        try {
            const response = await fetch("https://localhost:7003/api/conversations", {
                method: "GET",
                headers: { "Content-Type": "application/json" }
            });
            if (!response.ok) throw new Error(`API failed: ${await response.text()} (Status: ${response.status})`);
            const data = await response.json();
            console.log("Conversations data:", data);
            const conversations = data.conversations;
            conversationList.innerHTML = "";
            conversations.forEach(conv => {
                const li = document.createElement("li");
                li.className = "p-2 border-bottom border-white border-opacity-25";
                li.innerHTML = `
                    <a href="#" class="d-flex justify-content-between text-white conversation-item" data-conversation-key="${conv.ConversationKey}">
                        <div class="d-flex">
                            <img src="${conv.Avatar || '/images/avatar/default-avatar.jpg'}" alt="avatar" class="rounded-circle me-3" style="width: 48px; height: 48px;">
                            <div>
                                <p class="fw-bold mb-0">${conv.DisplayName || "Unknown"}</p>
                                <p class="small mb-0">${conv.LastMessage || "No messages"}</p>
                            </div>
                        </div>
                        <div class="text-end">
                            <p class="small mb-1">${conv.LastMessageTime ? formatTime(conv.LastMessageTime) : ""}</p>
                            ${conv.UnreadCount > 0 ? `<span class="badge bg-danger rounded-pill px-2">${conv.UnreadCount}</span>` : ''}
                        </div>
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
            console.log("Calling search API with query:", query);
            const response = await fetch(`/api/conversations/search?query=${encodeURIComponent(query)}`, {
                method: "GET",
                headers: { "Content-Type": "application/json" }
            });
            if (!response.ok) throw new Error(`Search failed: ${await response.text()} (Status: ${response.status})`);
            const results = await response.json();
            console.log("Search results:", results);
            searchResults.innerHTML = "";
            if (results.length === 0) {
                searchResults.innerHTML = `<div class="no-results">Not Found</div>`;
            } else {
                results.forEach(result => {
                    const div = document.createElement("div");
                    div.className = "search-result-item";
                    div.innerHTML = `
                        <img src="${result.Avatar || '/images/avatar/default-avatar.jpg'}" alt="${result.Name}" class="rounded-circle">
                        <p>${result.Name}</p>
                    `;
                    // Ngăn blur và xử lý chọn contact
                    div.addEventListener("mousedown", (e) => e.preventDefault());
                    div.addEventListener("click", (e) => {
                        e.stopPropagation();
                        selectContact(result);
                    });
                    searchResults.appendChild(div);
                });
            }
            searchResults.classList.add("show");
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
        headerAvatar.src = contact.Avatar || '/images/avatar/default-avatar.jpg';
        headerName.textContent = contact.Name || "Unknown";
        chatHeader.style.display = "flex";
        messageList.innerHTML = "";
        searchInput.value = "";
        searchResults.classList.remove("show");
        conversationListContainer.classList.remove("focused");
    }

    function addConversationClickListeners() {
        document.querySelectorAll(".conversation-item").forEach(item => {
            item.addEventListener("click", async (e) => {
                e.preventDefault();
                currentConversationKey = item.getAttribute("data-conversation-key");
                currentUserKey = null;
                currentUserType = null;
                const conv = Array.from(conversationList.children).find(li => li.querySelector(".conversation-item") === item).querySelector(".conversation-item");
                headerAvatar.src = conv.querySelector("img").src;
                headerName.textContent = conv.querySelector("p.fw-bold").textContent;
                chatHeader.style.display = "flex";
                document.querySelectorAll(".conversation-item").forEach(i => i.parentElement.classList.remove("active"));
                item.parentElement.classList.add("active");
                messageList.innerHTML = "";
                await loadMessages(currentConversationKey);
            });
        });
    }

    async function loadMessages(conversationKey) {
        console.log(`Loading messages for conversation: ${conversationKey}`);
    }

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

    if (fileIcon) {
        fileIcon.addEventListener("click", () => fileInput.click());
    }

    if (fileInput) {
        fileInput.addEventListener("change", (e) => {
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
    }

    if (clearFile) {
        clearFile.addEventListener("click", resetFileInput);
    }

    if (sendIcon) {
        sendIcon.addEventListener("click", async () => {
            const messageText = chatInput.value.trim();
            if (messageText || selectedFile) {
                if (currentConversationKey || currentUserKey) {
                    try {
                        const formData = new FormData();
                        formData.append("ConversationKey", currentConversationKey || "");
                        formData.append("UserKey", currentUserKey || "");
                        formData.append("UserType", currentUserType || "");
                        formData.append("Content", messageText);
                        if (selectedFile) formData.append("File", selectedFile);

                        const response = await fetch("/api/messages", {
                            method: "POST",
                            body: formData
                        });
                        if (!response.ok) {
                            throw new Error("Send message failed");
                        }
                        chatInput.value = "";
                        resetFileInput();
                        messageList.scrollTop = messageList.scrollHeight;
                        await loadMessages(currentConversationKey || null);
                    } catch (err) {
                        console.error("Send message failed:", err);
                    }
                }
            }
        });
    }

    if (openChat) {
        openChat.addEventListener("click", () => {
            if (chatModal) {
                $(chatModal).modal("show");
                unreadCount = 0;
                updateUnreadCount(unreadCount);
                loadConversations();
            }
        });
    }

    if (closeChat) {
        closeChat.addEventListener("click", () => {
            if (chatModal) {
                $(chatModal).modal("hide");
            }
        });
    }

    connection.on("ReceiveMessage", (message) => {
        if ((currentConversationKey && message.ConversationKey === currentConversationKey) || (currentUserKey && message.SenderKey === currentUserKey)) {
            const li = document.createElement("li");
            li.className = "d-flex mb-4";
            const div = document.createElement("div");
            div.className = "mask-custom rounded p-2";
            div.style.display = "inline-block";
            div.style.maxWidth = "fit-content";
            div.innerHTML = `
                <div class="d-flex align-items-center mb-2">
                    <img src="${message.Avatar || '/images/avatar/default-avatar.jpg'}" alt="avatar" class="rounded-circle me-2" style="width: 32px; height: 32px;">
                    <p class="fw-bold mb-0">${message.SenderName || message.SenderKey || "Unknown"}</p>
                </div>
                <hr class="border-white border-opacity-25 my-1">
                <div class="message-content">
                    <p class="mb-0">${message.Content || "No content"}</p>
                </div>
            `;
            li.appendChild(div);
            const timeDiv = document.createElement("div");
            timeDiv.className = "text-center my-2";
            timeDiv.innerHTML = `<p class="small text-muted">${formatTime(message.CreatedOn)}</p>`;
            messageList.appendChild(timeDiv);
            messageList.appendChild(li);
            messageList.scrollTop = messageList.scrollHeight;
        }
        unreadCount++;
        updateUnreadCount(unreadCount);
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

        const debouncedSearch = debounce(async (query) => {
            if (isSearching) return;
            isSearching = true;
            await searchContacts(query);
            isSearching = false;
        }, 500);

        searchInput.addEventListener("input", (e) => {
            const query = e.target.value.trim();
            debouncedSearch(query);
        });
    }
});