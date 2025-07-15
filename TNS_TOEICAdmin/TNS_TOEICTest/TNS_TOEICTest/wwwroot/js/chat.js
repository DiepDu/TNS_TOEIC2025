document.addEventListener("DOMContentLoaded", async () => {
    if (!window.signalR) {
        console.error("[DOMContentLoaded] SignalR không được tải!");
        return;
    }

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
        else console.warn("[DOMContentLoaded] Không lấy được MemberKey:", await response.text());
    } catch (error) {
        console.error("[DOMContentLoaded] Lỗi khi lấy MemberKey:", error);
    }

    if (!memberKey) {
        console.warn("[DOMContentLoaded] Không tìm thấy MemberKey. Tắt chat.");
        document.getElementById("openChat")?.addEventListener("click", () => alert("Vui lòng đăng nhập."));
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

    let blockPopup = null;

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
            console.warn("[attachIconListeners] Không tìm thấy iconBlock");
            return;
        }

        console.log("[attachIconListeners] Gắn sự kiện cho iconBlock");
        blockIcon.replaceWith(blockIcon.cloneNode(true));
        const newBlockIcon = document.getElementById("iconBlock");
        newBlockIcon.classList.add("icon-hover");
        newBlockIcon.style.cursor = "pointer";

        newBlockIcon.addEventListener("mouseenter", () => {
            console.log(`[IconHover] Hover vào iconBlock lúc:`, new Date().toISOString());
        });
        newBlockIcon.addEventListener("mouseleave", () => {
            console.log(`[IconHover] Rời chuột khỏi iconBlock lúc:`, new Date().toISOString());
        });

        newBlockIcon.addEventListener("click", debounce((e) => {
            console.log("[iconBlock] Nhấn icon block lúc:", new Date().toISOString());
            if (blockPopup) {
                console.log("[iconBlock] Xóa popup hiện có");
                blockPopup.remove();
                blockPopup = null;
                return;
            }

            blockPopup = document.createElement("div");
            blockPopup.id = "blockPopup";
            blockPopup.innerHTML = `
                <div class="popup-dialog">
                    <p class="popup-title">Bạn muốn làm gì?</p>
                    <button class="btn btn-danger w-100 mb-2" id="btnDeleteConversation">Xóa cuộc trò chuyện</button>
                    <button class="btn btn-warning w-100 mb-2" id="btnBlockUser">Chặn</button>
                    <button class="btn btn-secondary w-100" id="btnCancelBlock">Hủy</button>
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
            console.log("[iconBlock] Popup được tạo và thêm vào DOM");

            document.getElementById("btnDeleteConversation").addEventListener("click", async () => {
                console.log("[blockPopup] Nhấn Xóa cuộc trò chuyện");
                try {
                    await fetch(`/api/ChatController/DeleteConversation/${currentConversationKey}`, {
                        method: 'POST',
                        credentials: 'include'
                    });
                    console.log("[blockPopup] Đã xóa cuộc trò chuyện");
                    resetChatInterface();
                    loadConversations();
                } catch (err) {
                    console.error("[blockPopup] Lỗi xóa cuộc trò chuyện:", err);
                }
                blockPopup.remove();
                blockPopup = null;
            });

            document.getElementById("btnBlockUser").addEventListener("click", async () => {
                console.log("[blockPopup] Nhấn Chặn người dùng");
                try {
                    await fetch(`/api/ChatController/BlockUser/${currentConversationKey}`, {
                        method: 'POST',
                        credentials: 'include'
                    });
                    console.log("[blockPopup] Đã chặn người dùng");
                    resetChatInterface();
                    loadConversations();
                } catch (err) {
                    console.error("[blockPopup] Lỗi chặn người dùng:", err);
                }
                blockPopup.remove();
                blockPopup = null;
            });

            document.getElementById("btnCancelBlock").addEventListener("click", () => {
                console.log("[blockPopup] Nhấn Hủy");
                blockPopup.remove();
                blockPopup = null;
            });

            const outsideClickHandler = (event) => {
                if (blockPopup && !blockPopup.contains(event.target) && event.target !== newBlockIcon && !newBlockIcon.contains(event.target)) {
                    console.log("[blockPopup] Nhấn ngoài, xóa popup");
                    blockPopup.remove();
                    blockPopup = null;
                    document.removeEventListener("click", outsideClickHandler);
                }
            };
            setTimeout(() => {
                document.addEventListener("click", outsideClickHandler);
            }, 100);
        }, 100));

        const iconIds = ["iconCall", "iconVideo", "iconSetting"];
        iconIds.forEach(id => {
            const el = document.getElementById(id);
            if (el) {
                el.classList.add("icon-hover");
                el.style.cursor = "pointer";
                console.log(`[attachIconListeners] Đã thêm class icon-hover và cursor cho ${id}`);
                el.addEventListener("click", (e) => {
                    console.log(`[IconClick] Nhấn vào ${id} lúc:`, new Date().toISOString());
                });
                el.addEventListener("mouseenter", () => {
                    console.log(`[IconHover] Hover vào ${id} lúc:`, new Date().toISOString());
                });
                el.addEventListener("mouseleave", () => {
                    console.log(`[IconHover] Rời chuột khỏi ${id} lúc:`, new Date().toISOString());
                });
            } else {
                console.warn(`[attachIconListeners] Không tìm thấy phần tử với ID ${id}`);
            }
        });
    }

    function updateIconsVisibility() {
        const blockIcon = document.getElementById("iconBlock");
        const settingIcon = document.getElementById("iconSetting");
        console.log("[updateIconsVisibility] Loại cuộc trò chuyện:", currentConversationType);
        console.log("[updateIconsVisibility] Icon block tồn tại:", !!blockIcon, "Icon setting tồn tại:", !!settingIcon);
        if (blockIcon && settingIcon) {
            if (currentConversationType === "Private") {
                blockIcon.style.display = "inline-block";
                settingIcon.style.display = "none";
                console.log("[updateIconsVisibility] Hiển thị icon block cho chat riêng");
            } else if (currentConversationType === "Group") {
                blockIcon.style.display = "none";
                settingIcon.style.display = "inline-block";
                console.log("[updateIconsVisibility] Hiển thị icon setting cho chat nhóm");
            } else {
                blockIcon.style.display = "none";
                settingIcon.style.display = "none";
                console.log("[updateIconsVisibility] Ẩn cả hai icon do không xác định loại cuộc trò chuyện");
            }
            attachIconListeners();
        } else {
            console.error("[updateIconsVisibility] Không tìm thấy iconBlock hoặc iconSetting");
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
        console.log("[resetChatInterface] Làm mới giao diện chat");
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
        console.log("[updatePinnedSection] Cập nhật header tin nhắn ghim");
        const pinnedMessages = allMessages.filter(m => m.IsPinned);
        const firstPinned = pinnedMessages[0];
        const headerText = firstPinned ? (firstPinned.Content || `Ghim ${firstPinned.MessageType || 'Mục'}`) : "Chưa có tin nhắn ghim";
        pinnedSection.innerHTML = `<p>${headerText} (${pinnedMessages.length}/3 ghim)</p>`;
        pinnedSection.style.display = pinnedMessages.length > 0 ? "block" : "none";
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
            if (!response.ok) throw new Error(`[loadConversations] API thất bại: ${await response.text()} (Status: ${response.status})`);
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
                            <div><p class="fw-bold mb-0">${conv.DisplayName || "Không xác định"}</p><p class="small mb-0">${conv.LastMessage || "Chưa có tin nhắn"}</p></div>
                        </div>
                        <div class="text-end"><p class="small mb-1">${conv.LastMessageTime ? formatTime(conv.LastMessageTime) : ""}</p>${conv.UnreadCount > 0 ? `<span class="badge bg-danger rounded-pill px-2">${conv.UnreadCount}</span>` : ''}</div>
                    </a>
                `;
                conversationList.appendChild(li);
            });
            addConversationClickListeners();
        } catch (err) {
            console.error("[loadConversations] Lỗi tải danh sách cuộc trò chuyện:", err);
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
            if (!response.ok) throw new Error(`[searchContacts] Tìm kiếm thất bại: ${await response.text()} (Status: ${response.status})`);
            const results = await response.json();
            searchResults.innerHTML = results.length === 0 ? `<div class="no-results">Không tìm thấy</div>` : results.map(result => `
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
            console.error("[searchContacts] Lỗi tìm kiếm:", err);
            searchResults.innerHTML = `<div class="no-results">Không tìm thấy</div>`;
            searchResults.classList.add("show");
        }
    }

    function selectContact(contact) {
        console.log("[selectContact] Chọn liên hệ:", contact);
        currentConversationKey = contact.ConversationKey || null;
        currentUserKey = contact.UserKey || null;
        currentUserType = contact.UserType || null;
        currentConversationType = contact.ConversationType || null;
        headerAvatar.src = contact.Avatar || '/images/avatar/default-avatar.jpg';
        headerName.textContent = contact.Name || "Không xác định";
        chatHeaderInfo.style.display = "flex";
        chatHeaderContent.style.display = "block";
        messageList.innerHTML = "";
        searchInput.value = "";
        searchResults.classList.remove("show");
        conversationListContainer.classList.remove("focused");
        skip = 0;
        allMessages = [];

        // Chỉ highlight nếu liên hệ có ConversationKey khớp với danh sách
        document.querySelectorAll(".conversation-item").forEach(i => i.parentElement.classList.remove("active"));
        if (currentConversationKey) {
            const matchingConv = document.querySelector(`.conversation-item[data-conversation-key="${currentConversationKey}"]`);
            if (matchingConv) {
                matchingConv.parentElement.classList.add("active");
            }
        }

        updatePinnedSection();
        if (currentConversationKey) loadMessages(currentConversationKey);
        updateIconsVisibility();
    }

    function addConversationClickListeners() {
        document.querySelectorAll(".conversation-item").forEach(item => {
            item.addEventListener("click", (e) => {
                e.preventDefault();
                console.log("[addConversationClickListeners] Nhấn vào cuộc trò chuyện:", item);
                const conversationKey = item.getAttribute("data-conversation-key");
                const userKey = item.getAttribute("data-user-key") || null;
                const userType = item.getAttribute("data-user-type") || null;
                const conversationType = item.getAttribute("data-conversation-type") || null;

                currentConversationKey = conversationKey;
                currentUserKey = userKey;
                currentUserType = userType;
                currentConversationType = conversationType;
                console.log("[addConversationClickListeners] Cuộc trò chuyện được chọn - Key:", conversationKey, "Loại:", conversationType);

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
                loadMessages(currentConversationKey);
                updateIconsVisibility();
            });
        });
    }

    async function loadMessages(conversationKey, append = false) {
        if (!conversationKey) return;
        const url = `/api/conversations/messages/${conversationKey}?skip=${skip}&memberKey=${encodeURIComponent(memberKey)}`;
        try {
            const response = await fetch(url);
            if (!response.ok) throw new Error(`[loadMessages] Tải tin nhắn thất bại: ${await response.text()}`);
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
            console.error("[loadMessages] Lỗi tải tin nhắn:", err);
        }
    }

    async function loadMessageUntilFound(messageKey) {
        let currentSkip = skip;
        while (true) {
            const url = `/api/conversations/messages/${currentConversationKey}?skip=${currentSkip}&memberKey=${encodeURIComponent(memberKey)}`;
            const response = await fetch(url);
            if (!response.ok) throw new Error(`[loadMessageUntilFound] Tải tin nhắn thất bại: ${await response.text()}`);
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
            const contentHtml = m.Content ? `<p class="content" data-message-key="${m.MessageKey}">${m.Content}</p>` : '<p class="content" data-message-key="${m.MessageKey}">Không có nội dung</p>';

            return `
                <div>
                    <div class="pinned-message-container">
                        <div class="pinned-message ${isOwn ? 'right' : ''}">
                            <div class="message-box">
                                ${contentHtml}
                            </div>
                        </div>
                        <button class="pinned-unpin-btn" data-message-key="${m.MessageKey}">Bỏ ghim</button>
                    </div>
                </div>
            `;
        }).join("") || "<div>Chưa có tin nhắn ghim</div>";

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
                await fetch(`/api/conversations/unpin/${messageKey}`, {
                    method: "PUT"
                });

                allMessages = allMessages.map(m => m.MessageKey === messageKey ? { ...m, IsPinned: false } : m);
                updatePinnedSection();
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
        const senderName = isOwn ? "Bạn" : (message.SenderName || "Không xác định");
        const senderAvatar = isOwn ? "" : `<img src="${message.SenderAvatar || '/images/avatar/default-avatar.jpg'}" class="avatar">`;
        const time = formatTime(message.CreatedOn);
        const status = isOwn ? (message.Status === 0 ? '✔' : '✔✔') : '';

        let html = `<li class="message ${isOwn ? 'right' : 'left'} ${message.MessageType ? 'with-attachment' : ''} ${isRecalled ? 'recalled' : ''}" data-message-key="${message.MessageKey}">`;
        html += senderAvatar;

        html += `<div class="message-box">`;

        if (message.ParentMessageKey && message.ParentContent && typeof message.ParentContent === 'string' && message.ParentContent !== "[object Object]" && message.ParentContent.trim() !== "") {
            const displayParent = message.ParentStatus === 2 ? "Tin nhắn đã thu hồi" : (message.ParentContent === "Tin nhắn đã thu hồi" ? "Tin nhắn đã thu hồi" : message.ParentContent);
            html += `
                <div class="parent-message" data-parent-key="${message.ParentMessageKey}">
                    <p class="content">${displayParent}</p>
                </div>
            `;
        }

        html += `<div class="message-options"><i class="fas fa-ellipsis-h"></i></div>`;

        if (!isOwn && currentConversationType === 'Group') {
            html += `<p class="name">${senderName}</p><hr>`;
        }

        html += `<p class="content">${isRecalled ? "Tin nhắn đã thu hồi" : (message.Content || "")}</p>`;

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
        </li>`;
        return html;
    }

    document.addEventListener("click", function (e) {
        const parentEl = e.target.closest(".parent-message");
        if (parentEl) {
            const parentKey = parentEl.getAttribute("data-parent-key");
            const targetEl = document.querySelector(`[data-message-key="${parentKey}"]`);
            if (targetEl) {
                targetEl.scrollIntoView({ behavior: "smooth", block: "center" });
            } else {
                loadMessageUntilFound(parentKey);
            }
        }
    });

    messageList.addEventListener("scroll", debounce(() => {
        if (messageList.scrollTop === 0 && currentConversationKey) {
            loadMessages(currentConversationKey, true);
        }
    }, 300));

    async function startConnection() {
        try {
            await connection.start();
            console.log("[startConnection] Kết nối với ChatHub thành công");
            loadConversations();
        } catch (err) {
            console.error("[startConnection] Kết nối thất bại:", err);
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
                    if (!initResponse.ok) throw new Error("[sendIcon] Khởi tạo cuộc trò chuyện thất bại");
                    const initData = await initResponse.json();
                    currentConversationKey = initData.ConversationKey;
                    currentConversationType = initData.ConversationType || "Private";
                    updateIconsVisibility();
                }

                const response = await fetch("/api/conversations/messages", {
                    method: "POST",
                    body: formData
                });
                if (!response.ok) throw new Error("[sendIcon] Gửi tin nhắn thất bại");
                chatInput.value = "";
                resetFileInput();
                skip = 0;
                allMessages = [];
                await loadMessages(currentConversationKey);
            } catch (err) {
                console.error("[sendIcon] Lỗi gửi tin nhắn:", err);
            }
        }
    });
    if (openChat) openChat.addEventListener("click", () => {
        console.log("[openChat] Mở modal chat");
        $(chatModal).modal("show");
        unreadCount = 0;
        updateUnreadCount(unreadCount);
        loadConversations();
        updateIconsVisibility();
    });
    if (closeChat) closeChat.addEventListener("click", () => {
        console.log("[closeChat] Đóng modal chat");
        resetChatInterface();
        $(chatModal).modal("hide");
    });
    if (chatModal) {
        $(chatModal).on('hidden.bs.modal', () => {
            console.log("[chatModal] Modal đã đóng, làm mới giao diện");
            resetChatInterface();
        });
        $(chatModal).on('shown.bs.modal', () => {
            console.log("[chatModal] Modal hiển thị, cập nhật icon");
            updateIconsVisibility();
        });
    }

    connection.on("ReceiveMessage", (message) => {
        if ((currentConversationKey && message.ConversationKey === currentConversationKey) || (currentUserKey && message.SenderKey === currentUserKey)) {
            allMessages.push(message);
            messageList.insertAdjacentHTML("beforeend", addMessage(message));
            messageList.scrollTop = messageList.scrollHeight;
            unreadCount++;
            updateUnreadCount(unreadCount);
            updatePinnedSection();
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

    //if (chatHeaderContent) chatHeaderContent.addEventListener("click", showPinnedPopup);
    //if (pinnedSection) pinnedSection.addEventListener("click", showPinnedPopup);
    document.addEventListener("click", (e) => {
        const pinnedSection = document.getElementById("pinnedSection");
        const chatHeaderContent = document.getElementById("chatHeaderContent");

        if (
            pinnedSection &&
            pinnedSection.style.display !== "none" &&
            pinnedSection.contains(e.target)
        ) {
            console.log("Click vào bất kỳ đâu trong pinnedSection");
            showPinnedPopup();
            return;
        }

        if (
            chatHeaderContent &&
            chatHeaderContent.style.display !== "none" &&
            chatHeaderContent.contains(e.target)
        ) {
            console.log("Click vào bất kỳ đâu trong chatHeaderContent");
            showPinnedPopup();
        }
    });





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
            <div data-action="pin">Ghim</div>
            <div data-action="recall">Thu hồi</div>
        `;
        document.body.appendChild(menu);

        const rect = optionsButtonElement.getBoundingClientRect();
        menu.style.top = `${rect.bottom + window.scrollY}px`;
        menu.style.left = `${rect.left + window.scrollX}px`;

        menu.addEventListener('click', (e) => {
            if (e.target.dataset.action) {
                console.log(`[handleMessageOptionsClick] Hành động ${e.target.dataset.action} cho tin nhắn ${messageKey}`);
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
            updatePinnedSection();
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