async function startConnection(connection, memberKey) {
    try {
        console.log("Checking connection:", connection.state);
        if (connection.state !== signalR.HubConnectionState.Disconnected) {
            await connection.stop();
        }
        await connection.start();
        console.log("[startConnection] Connected to ChatHub successfully");
        await connection.invoke("InitializeConnection", null, memberKey);
        connection.on('Disconnected', () => {
            console.log("[Disconnected] Connection lost, attempting reconnect");
            setTimeout(() => startConnection(connection, memberKey), 2000);
        });
        window.connection.on("ReloadConversations", async (conversationKey) => {
            console.log(`Received ReloadConversations for conversation: ${conversationKey}`);
            if (typeof loadConversations === 'function') {
                await loadConversations();
            }
            if (String(currentConversationKey) === String(conversationKey)) {
                resetChatInterface();
            }
        });
        window.connection.on("MemberRemoved", (conversationKey, userKey, operatorName) => {
            if (String(currentConversationKey) === String(conversationKey)) {
                // Nếu người rời là người dùng hiện tại
                if (String(userKey) === String(memberKey)) {
                    window.resetChatInterface();
                    window.loadConversations();
                } else {
                    // Cập nhật chi tiết nhóm cho các thành viên còn lại
                    window.showGroupDetails(conversationKey).catch(() => {
                        // Bỏ qua lỗi 404/401 vì nhóm có thể đã bị xóa
                        console.log('[MemberRemoved] Group details not available, possibly deleted');
                    });
                }
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
                const listItem = document.querySelector(`.conversation-item[data-conversation-key="${conversationKey}"]`);
                if (listItem) {
                    const nameEl = listItem.querySelector('p.fw-bold') || listItem.querySelector('p') || listItem;
                    if (nameEl) {
                        nameEl.textContent = newGroupName;
                    }
                } else {
                    console.log("[UpdateGroupName] listItem not found");
                }
                const displayNameEl = document.getElementById('displayGroupName');
                if (displayNameEl) {
                    displayNameEl.textContent = newGroupName;
                } else {
                    const groupNameContainer = document.getElementById('groupName');
                    if (groupNameContainer) {
                        const p = groupNameContainer.querySelector('#displayGroupName');
                        if (p) p.textContent = newGroupName;
                        else groupNameContainer.textContent = newGroupName;
                        console.log("[UpdateGroupName] updated modal groupNameContainer");
                    }
                }
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
            if (String(currentConversationKey) === String(conversationKey)) {
                window.showGroupDetails(conversationKey);
            }
        });

        connection.on("MemberAdded", (conversationKey, userKey, operatorName) => {
            if (String(currentConversationKey) === String(conversationKey)) {
                window.showGroupDetails(conversationKey);
            }
        });
    } catch (err) {
        console.error("[startConnection] Connection failed:", err);
        setTimeout(() => startConnection(connection, memberKey), 5000);
    }
}

function updateUnreadCount(count) {
    const badge = document.getElementById("unreadCount");
    if (badge) {
        badge.textContent = count;
        badge.classList.toggle("d-none", count === 0);
    }
}
let unreadInterval;
let currentConversationKey = null;
const updatedGroupAvatars = {};

document.addEventListener("DOMContentLoaded", async () => {
    if (!window.signalR) {
        console.error("[DOMContentLoaded] SignalR not loaded!");
        return;
    }

    let unreadCount = 0;
    let currentUserKey = null;
    let currentUserType = null;
    let currentConversationType = null;
    let skip = 0;
    let allMessages = [];
    let memberKey = null;
    let parentMessageKeyForReply = null;
    let parentMessageContentForReply = null;
    let parentSenderNameForReply = null;
    let selectedFile = null;
    let markAsReadTimer = null;
    let unreadMessageKeysInActiveChat = []; 

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

    let blockIconAbortController = new AbortController();

    function applyBlockUI(isBlocked) {
        const iconBlock = document.getElementById("iconBlock");
        if (!iconBlock) return;
        console.log("[applyBlockUI] Applying state:", isBlocked, "Current classes:", iconBlock.classList.value);
        if (isBlocked) {
            iconBlock.classList.add("highlighted");
            iconBlock.setAttribute("title", "User is blocked. Click to unblock.");
        } else {
            iconBlock.classList.remove("highlighted");
            iconBlock.setAttribute("title", "Block this user.");
        }
    }

    // Hàm khởi tạo và gắn các sự kiện cho icon
    function attachIconListeners() {
        const blockIcon = document.getElementById("iconBlock");
        if (!blockIcon) return; // Nếu không tìm thấy icon thì dừng lại

        // 1. Gỡ bỏ event listener cũ một cách an toàn
        blockIconAbortController.abort();
        blockIconAbortController = new AbortController(); // Tạo controller mới cho lần gắn tiếp theo

        // Thêm các class và thuộc tính cần thiết
        blockIcon.classList.add("icon-hover");
        blockIcon.style.cursor = "pointer";

        // 2. Cập nhật trạng thái ban đầu của icon khi mở hội thoại
        const isInitiallyBlocked = window.currentConversationDetails ? window.currentConversationDetails.IsBanned : false;
        applyBlockUI(isInitiallyBlocked);

        // 3. Gắn sự kiện click mới với AbortSignal
        blockIcon.addEventListener("click", () => {
            // Xóa popup cũ nếu có
            const oldPopup = document.getElementById("blockConfirmationPopup");
            if (oldPopup) oldPopup.remove();

            // Kiểm tra dữ liệu cuộc hội thoại
            if (!window.currentConversationDetails || !window.currentConversationDetails.Participants) {
              
                showNotification("Conversation data not ready.", "error");
                return;
            }

            const isCurrentlyBanned = window.currentConversationDetails.IsBanned || false;

            // Tạo popup xác nhận
            const confirmationPopup = document.createElement("div");
            confirmationPopup.id = "blockConfirmationPopup";
            confirmationPopup.innerHTML = `
            <div class="popup-dialog">
                <p class="popup-title">${isCurrentlyBanned ? "Are you sure you want to unblock this person?" : "Are you sure you want to block this person?"}</p>
                <div class="popup-buttons">
                    <button class="btn btn-success" id="btnConfirmBlockAction">Có</button>
                    <button class="btn btn-secondary" id="btnCancelBlockAction">Không</button>
                </div>
            </div>`;

            // CSS cho popup
            Object.assign(confirmationPopup.style, {
                position: "absolute", top: "60px", right: "20px", zIndex: "1050",
                background: "#2c3e50", padding: "16px", borderRadius: "12px",
                border: "1px solid #3498db", boxShadow: "0 4px 12px rgba(0,0,0,0.25)",
                minWidth: "220px", color: "white", pointerEvents: "auto"
            });
            const popupButtons = confirmationPopup.querySelector(".popup-buttons");
            if (popupButtons) {
                popupButtons.style.display = "flex";
                popupButtons.style.justifyContent = "space-around";
                popupButtons.style.marginTop = "15px";
            }

            document.getElementById("chatModal").appendChild(confirmationPopup);

            // Sự kiện cho nút "Có"
            document.getElementById("btnConfirmBlockAction").addEventListener("click", async () => {
                // Tìm đối tác trong cuộc hội thoại 1-1
                const partner = window.currentConversationDetails.Participants.find(p => p.UserKey !== memberKey);
                if (!partner) {
                    showNotification("Unable to identify the opposing user.", "error");
                    confirmationPopup.remove();
                    return;
                }

                try {
                    // Gọi API để thay đổi trạng thái block
                    const response = await fetch(`/api/conversations/ToggleBlockUser/${currentConversationKey}`, {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        credentials: "include",
                        body: JSON.stringify({ TargetUserKey: partner.UserKey })
                    });
                    const result = await response.json();

                    // Xử lý kết quả trả về từ API
                    if (response.ok && result.success) {
                        showNotification(result.message, "success");

                        // --- BẮT ĐẦU PHẦN SỬA LỖI QUAN TRỌNG ---
                        if (window.currentConversationDetails) {
                            // 1. Xác định trạng thái block mới (ngược lại với trạng thái hiện tại)
                            const newBlockState = !window.currentConversationDetails.IsBanned;

                            // 2. Cập nhật trạng thái cho đối tượng cuộc hội thoại đang xem
                            window.currentConversationDetails.IsBanned = newBlockState;

                            // 3. Tìm và cập nhật trạng thái trong danh sách tổng (quan trọng để "ghi nhớ")
                            const conversationInMasterList = window.allConversations.find(
                                c => String(c.ConversationKey) === String(currentConversationKey)
                            );
                            if (conversationInMasterList) {
                                conversationInMasterList.IsBanned = newBlockState;
                            }

                            // 4. Cập nhật giao diện (icon) theo trạng thái mới
                            applyBlockUI(newBlockState);
                           
                        }
                        // --- KẾT THÚC PHẦN SỬA LỖI QUAN TRỌNG ---

                    } else {
                        throw new Error(result.message || "Error calling API");
                    }
                } catch (err) {
                  
                    showNotification(err.message || "Đã có lỗi xảy ra.", "error");
                } finally {
                    // Luôn đóng popup sau khi hoàn tất
                    confirmationPopup.remove();
                }
            });

            // Sự kiện cho nút "Không"
            document.getElementById("btnCancelBlockAction").addEventListener("click", () => {
                confirmationPopup.remove();
            });

            // Đóng popup khi click ra ngoài
            const outsideClickHandler = (event) => {
                if (confirmationPopup && !confirmationPopup.contains(event.target) && event.target !== blockIcon) {
                    confirmationPopup.remove();
                    document.removeEventListener("click", outsideClickHandler);
                }
            };
            setTimeout(() => document.addEventListener("click", outsideClickHandler), 50);

        }, { signal: blockIconAbortController.signal }); // Gắn signal vào listener
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
    window.resetChatInterface = resetChatInterface;
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
            if (!response.ok) {
                throw new Error(`[loadConversations] API failed: ${await response.text()} (Status: ${response.status})`);
            }

            const responseData = await response.json();
            const conversations = responseData.conversations || responseData;

            if (!Array.isArray(conversations)) {
                console.error("API did not return a valid array of conversations.", responseData);
                return;
            }

            // Gán dữ liệu vào biến toàn cục
            window.allConversations = conversations;

            console.log("Successfully stored conversation details.", window.allConversations);

            conversationList.innerHTML = "";

            conversations.sort((a, b) => {
                const timeA = a.LastMessageTime ? new Date(a.LastMessageTime) : new Date(0);
                const timeB = b.LastMessageTime ? new Date(b.LastMessageTime) : new Date(0);
                return timeB - timeA;
            });

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
    async function selectContact(contact) {
        currentConversationKey = contact.ConversationKey || null;
        currentUserKey = contact.UserKey || null;
        currentUserType = contact.UserType || null;
        currentConversationType = contact.ConversationType || null;
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
        // Sửa lỗi: Gán window.currentConversationDetails từ window.allConversations nếu có
        if (currentConversationKey) {
            const conversationDetails = window.allConversations.find(c => String(c.ConversationKey) === String(currentConversationKey));
            if (conversationDetails) {
                window.currentConversationDetails = conversationDetails;
                // Đối với cuộc hội thoại 1-1 (Private), tự xây dựng Participants nếu chưa có
                if (currentConversationType === "Private" && !conversationDetails.Participants) {
                    window.currentConversationDetails.Participants = [
                        { UserKey: memberKey },
                        { UserKey: currentUserKey }
                    ];
                }
            } else {
                console.warn("[selectContact] Conversation details not found in window.allConversations");
            }
        }
        updatePinnedSection();
        updateIconsVisibility();

        // Thêm: Gọi API để lấy trạng thái IsBanned của đối phương và cập nhật icon
        if (currentConversationType === "Private" && currentUserKey) {
            try {
                const response = await fetch(`/api/conversations/GetBanStatus?conversationKey=${currentConversationKey}&targetUserKey=${currentUserKey}`);
                if (response.ok) {
                    const data = await response.json();
                    if (data.success) {
                        window.currentConversationDetails.IsBanned = data.isBanned;
                        applyBlockUI(data.isBanned);
                        console.log(`[GetBanStatus] Updated IsBanned for ${currentUserKey}: ${data.isBanned}`);
                    } else {
                        console.warn("[GetBanStatus] API returned failure:", data.message);
                    }
                } else {
                    console.error("[GetBanStatus] API call failed:", response.status);
                }
            } catch (err) {
                console.error("[GetBanStatus] Error fetching ban status:", err);
            }
        }
    }

    // Thay thế toàn bộ hàm addConversationClickListeners trong chat.js
    function addConversationClickListeners() {
        document.querySelectorAll(".conversation-item").forEach(item => {
            item.addEventListener("click", async (e) => {
                e.preventDefault();
                const conversationKey = item.getAttribute("data-conversation-key");
                // Di chuyển các const lên đầu để sử dụng trong phần sửa lỗi
                const userKey = item.getAttribute("data-user-key") || null;
                const userType = item.getAttribute("data-user-type") || null;
                const conversationType = item.getAttribute("data-conversation-type") || null;

                // --- BẮT ĐẦU PHẦN BỔ SUNG: ĐÁNH DẤU ĐÃ ĐỌC ---
                const badge = item.querySelector(".badge");
                if (badge) {
                    const unreadCount = parseInt(badge.textContent, 10);
                    if (unreadCount > 0) {
                        // Ẩn badge ngay trên giao diện để phản hồi nhanh
                        badge.remove();

                        // Cập nhật tổng số tin chưa đọc
                        try {
                            const totalBadge = document.getElementById("unreadCount");
                            let currentTotal = parseInt(totalBadge.textContent, 10) || 0;
                            currentTotal -= unreadCount;
                            updateUnreadCount(Math.max(0, currentTotal));
                        } catch (err) {
                            console.error("Failed to update total unread count on UI", err);
                        }

                        // Gọi API ở chế độ nền
                        fetch(`/api/conversations/markAsRead/${conversationKey}`, {
                            method: 'POST',
                            credentials: 'include'
                        }).then(response => {
                            if (!response.ok) {
                                console.error(`API markAsRead for ${conversationKey} failed.`);
                            } else {
                                console.log(`[MarkAsRead] Conversation ${conversationKey} marked as read.`);
                            }
                        }).catch(err => {
                            console.error(`[MarkAsRead] Error calling API for ${conversationKey}:`, err);
                        });
                    }
                }
                // --- KẾT THÚC PHẦN BỔ SUNG ---

                // --- BẮT ĐẦU PHẦN SỬA LỖI QUAN TRỌNG ---
                // 1. Kiểm tra xem danh sách toàn cục có tồn tại không
                if (!window.allConversations) {
                    console.error("CRITICAL: Conversation list (window.allConversations) is not available!");
                    showNotification("Error: Conversation list not loaded.", "error");
                    return;
                }
                // 2. Tìm chi tiết cuộc hội thoại từ danh sách đã lưu (so sánh dưới dạng chuỗi để đảm bảo an toàn)
                const conversationDetails = window.allConversations.find(c => String(c.ConversationKey) === String(conversationKey));
                // 3. Gán chi tiết vào biến toàn cục. Đây là bước khắc phục lỗi!
                window.currentConversationDetails = conversationDetails;

                // Sửa lỗi: Đối với cuộc hội thoại 1-1 (Private), tự xây dựng Participants nếu chưa có
                // Sử dụng conversationType (local) và userKey thay vì global variables chưa set
                if (conversationType === "Private" && conversationDetails && !conversationDetails.Participants) {
                    window.currentConversationDetails.Participants = [
                        { UserKey: memberKey },
                        { UserKey: userKey }
                    ];
                }
                // Debug log để kiểm tra (có thể xóa sau khi test ổn)
                console.log("Set details:", window.currentConversationDetails);
                // --- KẾT THÚC PHẦN SỬA LỖI QUAN TRỌNG ---
                // Tất cả logic cũ của bạn bên dưới được giữ nguyên và sẽ hoạt động đúng
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

                // Thêm: Gọi API để lấy trạng thái IsBanned của đối phương và cập nhật icon
                if (currentConversationType === "Private" && currentUserKey) {
                    try {
                        const response = await fetch(`/api/conversations/GetBanStatus?conversationKey=${conversationKey}&targetUserKey=${currentUserKey}`);
                        if (response.ok) {
                            const data = await response.json();
                            if (data.success) {
                                window.currentConversationDetails.IsBanned = data.isBanned;
                                applyBlockUI(data.isBanned);
                                console.log(`[GetBanStatus] Updated IsBanned for ${currentUserKey}: ${data.isBanned}`);
                            } else {
                                console.warn("[GetBanStatus] API returned failure:", data.message);
                            }
                        } else {
                            console.error("[GetBanStatus] API call failed:", response.status);
                        }
                    } catch (err) {
                        console.error("[GetBanStatus] Error fetching ban status:", err);
                    }
                }
            });
        });
    }

    async function loadMessages(conversationKey, append = false, skip) {
        if (!conversationKey) return;
        const url = `/api/conversations/messages/${conversationKey}?skip=${skip}&memberKey=${encodeURIComponent(memberKey)}`;
        try {
            const response = await fetch(url);
            if (!response.ok) {
                if (response.status === 401) {
                    console.log('[loadMessages] Access denied, user likely left the group');
                    return; // Bỏ qua lỗi 401
                }
                throw new Error(`[loadMessages] Failed to load messages: ${await response.text()}`);
            }
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



    // Thay thế toàn bộ hàm này trong chat.js

    // TÌM VÀ THAY THẾ TOÀN BỘ HÀM NÀY TRONG FILE chat.js

    function processIncomingMessage(rawMessage) {
        // 1) Chuẩn hóa tên thuộc tính
        const message = {
            ConversationKey: rawMessage.ConversationKey ?? rawMessage.conversationKey,
            MessageKey: rawMessage.MessageKey ?? rawMessage.messageKey,
            SenderKey: rawMessage.SenderKey ?? rawMessage.senderKey,
            SenderName: rawMessage.SenderName ?? rawMessage.senderName,
            SenderAvatar: rawMessage.SenderAvatar ?? rawMessage.senderAvatar,
            MessageType: rawMessage.MessageType ?? rawMessage.messageType,
            Content: rawMessage.Content ?? rawMessage.content,
            ParentMessageKey: rawMessage.ParentMessageKey ?? rawMessage.parentMessageKey,
            CreatedOn: rawMessage.CreatedOn ?? rawMessage.createdOn,
            Status: rawMessage.Status ?? rawMessage.status,
            IsPinned: rawMessage.IsPinned ?? rawMessage.isPinned,
            IsSystemMessage: rawMessage.IsSystemMessage ?? rawMessage.isSystemMessage,
            Url: rawMessage.Url ?? rawMessage.url,

            // --- BẮT ĐẦU PHẦN SỬA LỖI ---
            // Bổ sung 2 dòng bị thiếu để đọc thông tin tin nhắn cha
            ParentContent: rawMessage.ParentContent ?? rawMessage.parentContent,
            ParentStatus: rawMessage.ParentStatus ?? rawMessage.parentStatus
            // --- KẾT THÚC PHẦN SỬA LỖI ---
        };

        // Phần logic còn lại của hàm được giữ nguyên
        if (message.MessageKey && allMessages.some(m => m.MessageKey === message.MessageKey)) {
            return false;
        }
        allMessages.push(message);
        if (String(message.ConversationKey) === String(currentConversationKey)) {
            messageList.insertAdjacentHTML("beforeend", addMessage(message));

            // Logic Debounce đánh dấu đã đọc
            if (message.SenderKey !== memberKey) {
                unreadMessageKeysInActiveChat.push(message.MessageKey);
                clearTimeout(markAsReadTimer);
                markAsReadTimer = setTimeout(() => {
                    if (unreadMessageKeysInActiveChat.length > 0) {
                        const keysToSend = [...unreadMessageKeysInActiveChat];
                        unreadMessageKeysInActiveChat = [];
                        fetch('/api/conversations/markMessagesAsRead', {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            credentials: 'include',
                            body: JSON.stringify({
                                MessageKeys: keysToSend,
                                ConversationKey: currentConversationKey
                            })
                        }).then(response => {
                            if (response.ok) {
                                console.log(`[Debounce MarkAsRead] Sent ${keysToSend.length} message keys.`);
                            } else {
                                console.error('[Debounce MarkAsRead] API call failed.');
                            }
                        }).catch(err => console.error('[Debounce MarkAsRead] Fetch error:', err));
                    }
                }, 2000);
            }

        } else {
            const item = document.querySelector(`.conversation-item[data-conversation-key="${message.ConversationKey}"]`);
            if (item) {
                const lastMessageEl = item.querySelector("p.small.mb-0");
                const timeEl = item.querySelector("p.small.mb-1");
                lastMessageEl.textContent = message.Content || "New message";
                timeEl.textContent = formatTime(message.CreatedOn);
                const badge = item.querySelector(".badge");
                if (badge) {
                    badge.textContent = (parseInt(badge.textContent, 10) || 0) + 1;
                } else {
                    const newBadge = document.createElement("span");
                    newBadge.className = "badge bg-danger rounded-pill px-2";
                    newBadge.textContent = "1";
                    item.querySelector(".text-end")?.appendChild(newBadge);
                }
                unreadCount++;
                updateUnreadCount(unreadCount);
            }
        }

        return true;
    }

    window.connection.on("ReceiveMessage", (rawMessage) => {
        console.log("[ReceiveMessage] received", rawMessage);
        const hasNewMessage = processIncomingMessage(rawMessage);

        if (hasNewMessage) {
            if (String(rawMessage.ConversationKey ?? rawMessage.conversationKey) === String(currentConversationKey)) {
                setTimeout(() => { messageList.scrollTop = messageList.scrollHeight; }, 0);
            }
            updatePinnedSection?.();
        }
    });

    window.connection.on("MessagesRead", (conversationKey, readerUserKey) => {
        // Chỉ cập nhật nếu sự kiện này dành cho cuộc hội thoại đang mở
        // Và người đọc không phải là chính mình
        if (String(conversationKey) === String(currentConversationKey) && String(readerUserKey) !== String(memberKey)) {
            console.log(`[MessagesRead] User ${readerUserKey} has read messages in this conversation.`);

            // Lặp qua tất cả các tin nhắn của bạn trên màn hình có trạng thái 'đã gửi'
            document.querySelectorAll(`.message.right .status`).forEach(statusElement => {
                if (statusElement.textContent === '✔') {
                    statusElement.textContent = '✔✔';
                }
            });

            // Cập nhật trạng thái trong mảng dữ liệu allMessages
            allMessages.forEach(msg => {
                if (msg.SenderKey === memberKey && msg.Status === 0) {
                    msg.Status = 1;
                }
            });
        }
    });

    window.connection.on("ReceiveMultipleMessages", (messages) => {
        if (!messages || messages.length === 0) return;
        let newMessagesProcessed = 0;
        messages.forEach(rawMessage => {
            if (processIncomingMessage(rawMessage)) {
                newMessagesProcessed++;
            }
        });
        if (newMessagesProcessed > 0) {
            const lastMessage = messages[messages.length - 1];
            if (String(lastMessage.ConversationKey ?? lastMessage.conversationKey) === String(currentConversationKey)) {
                setTimeout(() => { messageList.scrollTop = messageList.scrollHeight; }, 0);
            }
            updatePinnedSection?.();
        }
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
    if (fileInput) {
        fileInput.addEventListener('change', e => {
            const file = e.target.files[0];
            if (file) {
                selectedFile = file;
                showFilePreview(file);
            }
        });
    }

    // BỔ SUNG CÁC HÀM TIỆN ÍCH MỚI
    function clearFilePreview() {
        const previewContainer = document.getElementById('file-preview-container');
        selectedFile = null;
        if (fileInput) fileInput.value = ''; // Quan trọng: reset input file
        previewContainer.innerHTML = '';
        previewContainer.style.display = 'none';
    }

    function showFilePreview(file) {
        const previewContainer = document.getElementById('file-preview-container');
        const fileType = file.type;
        let previewElement;

        const wrapper = document.createElement('div');
        wrapper.className = 'preview-wrapper';

        if (fileType.startsWith('image/')) {
            previewElement = document.createElement('img');
            previewElement.src = URL.createObjectURL(file);
        } else if (fileType.startsWith('video/')) {
            previewElement = document.createElement('video');
            previewElement.src = URL.createObjectURL(file);
            previewElement.controls = false;
        } else if (fileType.startsWith('audio/')) {
            previewElement = document.createElement('audio');
            previewElement.src = URL.createObjectURL(file);
            previewElement.controls = true;
        } else {
            previewElement = document.createElement('span');
            previewElement.className = 'preview-filename';
            previewElement.textContent = file.name;
        }

        previewElement.className += ' preview-item';

        const removeIcon = document.createElement('span');
        removeIcon.className = 'remove-preview-icon';
        removeIcon.innerHTML = '&times;';
        removeIcon.title = 'Remove file';
        removeIcon.onclick = clearFilePreview;

        wrapper.appendChild(previewElement);
        wrapper.appendChild(removeIcon);

        previewContainer.innerHTML = '';
        previewContainer.appendChild(wrapper);
        previewContainer.style.display = 'flex';
    }

    function clearReplyPreview() {
        const previewContainer = document.getElementById('reply-preview-container');
        parentMessageKeyForReply = null;
        parentMessageContentForReply = null;
        parentSenderNameForReply = null;
        previewContainer.innerHTML = '';
        previewContainer.style.display = 'none';
    }

    function showReplyPreview(key, content, senderName) {
        parentMessageKeyForReply = key;
        parentMessageContentForReply = content;
        parentSenderNameForReply = senderName;

        const previewContainer = document.getElementById('reply-preview-container');
        previewContainer.innerHTML = `
        <div class="preview-content">
            <span class="reply-user">Replying to ${senderName}</span>
            <span class="reply-text text-muted">${content}</span>
        </div>
        <span class="remove-preview-icon" title="Cancel reply">&times;</span>
    `;
        previewContainer.querySelector('.remove-preview-icon').onclick = clearReplyPreview;
        previewContainer.style.display = 'flex';
    }
    if (clearFile) clearFile.addEventListener("click", resetFileInput);
    if (sendIcon) {
        sendIcon.addEventListener("click", async () => {
            const content = chatInput.value.trim();

            if (!content && !selectedFile) {
                return; // Không có gì để gửi
            }

            if (!currentConversationKey) {
                showNotification("Please select a conversation first.", "error");
                return;
            }

            const formData = new FormData();
            // --- BẮT ĐẦU SỬA LỖI: Chuyển tên tham số sang chữ thường ---
            formData.append("conversationKey", currentConversationKey);
            formData.append("content", content);

            if (currentConversationType === 'Private' && currentUserKey) {
                formData.append("userKey", currentUserKey);
                formData.append("userType", currentUserType);
            }

            if (selectedFile) {
                formData.append("file", selectedFile);
            }

            if (parentMessageKeyForReply) {
                formData.append("parentMessageKey", parentMessageKeyForReply);
                formData.append("parentMessageContent", parentMessageContentForReply);
            }
            // --- KẾT THÚC SỬA LỖI ---

            const originalContent = chatInput.value;
            const replyingMessage = {
                key: parentMessageKeyForReply,
                content: parentMessageContentForReply,
                sender: parentSenderNameForReply
            };
            const sendingFile = selectedFile;

            chatInput.value = "";
            clearFilePreview();
            clearReplyPreview();

            try {
                const response = await fetch("/api/conversations/messages", {
                    method: "POST",
                    body: formData,
                    credentials: 'include'
                });

                if (!response.ok) {
                    const errorResult = await response.json().catch(() => ({ message: "Failed to send message and parse error." }));
                    throw new Error(errorResult.message || "Failed to send message.");
                }

                console.log("Message sent successfully, waiting for SignalR echo.");

            } catch (err) {
                console.error("[sendIcon] Error sending message:", err);
                showNotification(err.message, "error");

                // Khôi phục lại giao diện nếu gửi thất bại
                chatInput.value = originalContent;
                if (replyingMessage.key) {
                    showReplyPreview(replyingMessage.key, replyingMessage.content, replyingMessage.sender);
                }
                if (sendingFile) {
                    selectedFile = sendingFile;
                    showFilePreview(sendingFile);
                }
            } finally {
                chatInput.focus();
            }
        });
    }
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

    if (messageList) {
        messageList.addEventListener('click', (e) => {
            // --- Logic xử lý menu options (pin, recall, reply) ---
            const optionsButton = e.target.closest('.message-options');
            if (optionsButton) {
                const messageElement = optionsButton.closest('.message');
                const messageKey = messageElement.dataset.messageKey;
                const senderKey = messageElement.dataset.senderKey;

                const message = allMessages.find(m => m.MessageKey === messageKey);
                if (!message) return;

                // Xóa menu cũ nếu có
                const existingMenu = document.getElementById("messageOptionsMenu");
                if (existingMenu) existingMenu.remove();

                const isMyMessage = senderKey === memberKey;
                const isPinned = message.IsPinned;

                const menu = document.createElement("div");
                menu.id = "messageOptionsMenu";
                menu.className = "message-options-menu";
                // Tạo menu với các tùy chọn
                menu.innerHTML = `
                <div class="menu-item" data-action="reply">💬 Reply</div>
                <div class="menu-item" data-action="${isPinned ? 'unpin' : 'pin'}">${isPinned ? '📌 Unpin' : '📌 Pin'}</div>
                ${isMyMessage ? `<div class="menu-item" data-action="recall">↩️ Recall</div>` : ""}
            `;

                // Code hiển thị menu
                const modalContent = document.querySelector('#chatModal .modal-content');
                if (!modalContent) return;
                modalContent.appendChild(menu);
                const iconRect = optionsButton.getBoundingClientRect();
                const modalRect = modalContent.getBoundingClientRect();
                let top = iconRect.top - modalRect.top + optionsButton.offsetHeight;
                let left = iconRect.left - modalRect.left;
                Object.assign(menu.style, { position: "absolute", top: `${top}px`, left: `${left}px` });

                // Gắn sự kiện cho các item trong menu
                menu.querySelectorAll(".menu-item").forEach(item => {
                    item.addEventListener("click", (evt) => {
                        evt.stopPropagation();
                        const action = item.dataset.action;
                        menu.remove(); // Đóng menu sau khi chọn

                        if (action === "pin") pinMessage(messageKey);
                        if (action === "unpin") unpinMessage(messageKey);
                        if (action === "recall" && isMyMessage) recallMessage(messageKey);
                        if (action === "reply") {
                            const content = message.Content || (message.MessageType !== 'Text' ? message.MessageType : 'Attachment');
                            showReplyPreview(messageKey, content, message.SenderName);
                            document.getElementById('chatInput').focus();
                        }
                    });
                });

                // Logic ẩn menu khi click ra ngoài
                const hideMenu = (evt) => {
                    if (!menu.contains(evt.target) && !optionsButton.contains(evt.target)) {
                        menu.remove();
                        document.removeEventListener("click", hideMenu);
                    }
                };
                setTimeout(() => document.addEventListener("click", hideMenu), 0);
            }

            // --- Logic xử lý click vào tin nhắn cha để scroll ---
            const parentEl = e.target.closest(".parent-message");
            if (parentEl) {
                const parentKey = parentEl.getAttribute("data-parent-key");
                const targetEl = document.querySelector(`[data-message-key="${parentKey}"]`);
                if (targetEl) {
                    targetEl.scrollIntoView({ behavior: "smooth", block: "center" });
                } else {
                    loadMessageUntilFound(parentKey, skip);
                }
            }
        });
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
            alert("Error recalling message. Check console for details.");
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
        const leaveBtn = modalRoot.querySelector('.leave-group-btn');
        const addBtn = modalRoot.querySelector('.add-member-btn');
        if (leaveBtn) leaveBtn.onclick = () => showLeaveConfirmation(conversationKey);
        if (addBtn) addBtn.onclick = () => showAddMemberPopup(conversationKey);

        window.goupDetails_modal.show();
    } catch (err) {
        console.error('[showGroupDetails] Error:', err);
    }
};
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

function showLeaveConfirmation(conversationKey) {
    const modalContent = document.getElementById('group_details_modal_content');
    const detailsView = document.getElementById('group-details-view');
    const confirmationView = document.getElementById('remove-member-confirmation-view');

    if (!modalContent || !detailsView || !confirmationView) {
        console.error('[showLeaveConfirmation] Required DOM elements not found.');
        return;
    }

    confirmationView.className = 'confirmation-content';
    confirmationView.innerHTML = `
        <div class="p-2">
            <h4 class="confirmation-title">Confirm Leave Group</h4>
            <p style="font-size: 1.1em; color: #f0f0f0; text-align: center; margin: 20px 0;">
                Are you sure you want to leave this group?
            </p>
            <div class="confirmation-actions">
                <button class="btn btn-secondary cancel-leave">No</button>
                <button class="btn btn-danger confirm-leave">Yes</button>
            </div>
        </div>
    `;

    const confirmLeave = confirmationView.querySelector('.confirm-leave');
    const cancelLeave = confirmationView.querySelector('.cancel-leave');

    if (confirmLeave) {
        confirmLeave.addEventListener('click', async () => {
            await leaveGroup(conversationKey);
        });
    } else {
        console.error('[showLeaveConfirmation] .confirm-leave not found');
    }

    if (cancelLeave) {
        cancelLeave.addEventListener('click', () => {
            confirmationView.style.display = 'none';
            detailsView.style.display = 'block';
            window.showGroupDetails(conversationKey);
        });
    } else {
        console.error('[showLeaveConfirmation] .cancel-leave not found');
    }

    detailsView.style.display = 'none';
    confirmationView.style.display = 'block';
}

async function leaveGroup(conversationKey) {
    const detailsView = document.getElementById('group-details-view');
    const confirmationView = document.getElementById('remove-member-confirmation-view');

    try {
        const res = await fetch('/api/conversations/LeaveGroup', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include',
            body: JSON.stringify({ conversationKey })
        });
        const json = await res.json();
        console.log('[LeaveGroup] raw json:', json);

        const isSuccess = json && (json.success === true || String(json.success).toLowerCase() === 'true');
        if (!isSuccess) {
            showNotification(json?.message || 'Failed to leave group', 'error');
            return; // Không gọi showGroupDetails vì người dùng đã rời nhóm
        }

        // Hiển thị thông báo thành công
        showNotification('You have left the group successfully', 'success');

        // Đóng modal chi tiết nhóm
        if (window.goupDetails_modal) {
            window.goupDetails_modal.hide();
        }

        // Reset giao diện nội dung tin nhắn về trạng thái trống
        window.resetChatInterface();

        // Reload danh sách conversation trực tiếp
        await window.loadConversations();

    } catch (err) {
        console.error('[LeaveGroup] error:', err);
        showNotification('Error leaving group', 'error');
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

function __escHtml(s) {
    if (!s && s !== '') return '';
    return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

window.showAddMemberPopup = async function (conversationKey, preselectedKeys = []) {
    const detailsView = document.getElementById('group-details-view');
    const hostView = document.getElementById('remove-member-confirmation-view');
    if (!detailsView || !hostView) return;
    const excludeKeys = Array.from(document.querySelectorAll('#memberList .remove-member-icon'))
        .map(b => (b.getAttribute('data-user-key') || '').toString())
        .filter(Boolean);
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

    const listEl = hostView.querySelector('#addMembersList');
    const btnAdd = hostView.querySelector('#btnConfirmAdd');
    const btnCancel = hostView.querySelector('#btnCancelAdd');

    const getSelectedKeys = () =>
        Array.from(listEl.querySelectorAll('.add-item.selected'))
            .map(it => it.getAttribute('data-user-key'))
            .filter(Boolean);
    listEl.addEventListener('click', (e) => {
        const item = e.target.closest('.add-item');
        if (!item) return;
        const cb = item.querySelector('input[type="checkbox"]');
        const willSelect = !item.classList.contains('selected');
        item.classList.toggle('selected', willSelect);
        if (cb) cb.checked = willSelect;
        btnAdd.disabled = getSelectedKeys().length === 0;
    });
    listEl.addEventListener('change', (e) => {
        const cb = e.target;
        if (!(cb instanceof HTMLInputElement) || cb.type !== 'checkbox') return;
        const item = cb.closest('.add-item');
        if (!item) return;
        item.classList.toggle('selected', cb.checked);
        btnAdd.disabled = getSelectedKeys().length === 0;
    });
    btnCancel.addEventListener('click', () => {
        hostView.style.display = 'none';
        detailsView.style.display = 'block';
    });
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

function showAddMembersConfirmation(conversationKey, selected) {
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

            // Tin nhắn hệ thống sẽ được xử lý bởi handler ReceiveMessage
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

