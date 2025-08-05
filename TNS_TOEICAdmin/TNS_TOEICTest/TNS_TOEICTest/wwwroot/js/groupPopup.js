(function () {
    const addGroupIcon = document.getElementById("addGroupIcon");
    const createGroupPopup = document.getElementById("createGroupPopup");
    const closeCreateGroup = document.getElementById("closeCreateGroup");
    const avatarCircle = document.getElementById("avatarCircle");
    const avatarPreview = document.getElementById("avatarPreview");
    const groupNameInput = document.getElementById("groupNameInput");
    const groupSearchInput = document.getElementById("groupSearchInput");
    const userList = document.getElementById("userList");
    const selectedUsers = document.getElementById("selectedUsers");
    const createGroupBtn = document.getElementById("createGroupBtn");
    let selectedAvatar = null;
    let memberKey = null;
    let initialUsers = [];

    // Lắng nghe event từ chat.js
    window.addEventListener('openGroupPopup', (e) => {
        memberKey = e.detail.memberKey;
        if (addGroupIcon) {
            addGroupIcon.addEventListener("click", () => {
                createGroupPopup.style.display = "block";
                avatarPreview.classList.add("d-none");
                selectedAvatar = null;
                groupNameInput.value = "";
                selectedUsers.innerHTML = "";
                loadInitialUsers();
            });
        }
    });

    if (closeCreateGroup) {
        closeCreateGroup.addEventListener("click", () => {
            createGroupPopup.style.display = "none";
        });
    }

    if (avatarCircle) {
        avatarCircle.addEventListener("click", () => {
            const fileInput = document.createElement("input");
            fileInput.type = "file";
            fileInput.accept = "image/*";
            fileInput.addEventListener("change", (e) => {
                selectedAvatar = e.target.files[0];
                if (selectedAvatar && /\.(jpe?g|png)$/i.test(selectedAvatar.name)) {
                    avatarPreview.src = URL.createObjectURL(selectedAvatar);
                    avatarPreview.classList.remove("d-none");
                } else {
                    alert("Please select a valid image file (.jpg or .png)!");
                    selectedAvatar = null;
                    avatarPreview.classList.add("d-none");
                }
            });
            fileInput.click();
        });
    }

    async function loadInitialUsers() {
        if (!memberKey) {
            console.warn("MemberKey not available");
            return;
        }
        try {
            const response = await fetch(`/api/conversations/GetGroupMembers?memberKey=${encodeURIComponent(memberKey)}`);
            const users = await response.json();
            initialUsers = users;
            userList.innerHTML = users.map(user => `
                <div class="user-item" data-user-key="${user.UserKey}" data-user-type="${user.UserType}">
                    <img src="${user.Avatar || '/images/avatar/default-avatar.jpg'}" alt="User">
                    <span>${user.Name}</span>
                </div>
            `).join("");
            attachUserClickListeners(); // Sửa lỗi typo từ attachUserClick thành attachUserClickListeners
        } catch (error) {
            console.error("Error loading users:", error);
            userList.innerHTML = "<div>Error loading users</div>";
        }
    }

    function attachRemoveListeners() {
        document.querySelectorAll(".remove-btn").forEach(btn => {
            btn.addEventListener("click", (e) => {
                const userItem = e.target.parentElement;
                const userKey = userItem.getAttribute("data-user-key");
                const userType = userItem.getAttribute("data-user-type");
                const userName = userItem.querySelector("span").textContent;
                const userAvatar = userItem.querySelector("img").src;
                userList.innerHTML += `
                    <div class="user-item" data-user-key="${userKey}" data-user-type="${userType}">
                        <img src="${userAvatar}" alt="User">
                        <span>${userName}</span>
                    </div>
                `;
                userItem.remove();
                attachUserClickListeners();
            });
        });
    }

    function attachUserClickListeners() {
        document.querySelectorAll(".user-item").forEach(item => {
            item.addEventListener("click", () => {
                const userKey = item.getAttribute("data-user-key");
                const userType = item.getAttribute("data-user-type");
                const userName = item.querySelector("span").textContent;
                const userAvatar = item.querySelector("img").src;
                if (!selectedUsers.querySelector(`[data-user-key="${userKey}"]`)) {
                    selectedUsers.innerHTML += `
                        <div class="selected-user-item" data-user-key="${userKey}" data-user-type="${userType}">
                            <img src="${userAvatar}" alt="Selected User">
                            <span>${userName}</span>
                            <span class="remove-btn" style="font-size: 24px; font-weight: bold; cursor: pointer;">X</span>
                        </div>
                    `;
                    item.remove();
                }
                attachRemoveListeners();
            });
        });
    }

    if (groupSearchInput) {
        groupSearchInput.addEventListener("input", debounce((e) => {
            const query = e.target.value.trim().toLowerCase();
            if (initialUsers.length > 0) {
                let filteredUsers = [...initialUsers];
                if (query) {
                    filteredUsers.sort((a, b) => {
                        const aMatch = a.Name.toLowerCase().includes(query) ? a.Name.toLowerCase().indexOf(query) : Infinity;
                        const bMatch = b.Name.toLowerCase().includes(query) ? b.Name.toLowerCase().indexOf(query) : Infinity;
                        return aMatch - bMatch;
                    });
                }
                userList.innerHTML = filteredUsers.map(user => `
                    <div class="user-item" data-user-key="${user.UserKey}" data-user-type="${user.UserType}">
                        <img src="${user.Avatar || '/images/avatar/default-avatar.jpg'}" alt="User">
                        <span>${user.Name}</span>
                    </div>
                `).join("");
                attachUserClickListeners();
            }
        }, 300));
    }

    if (createGroupBtn) {
        createGroupBtn.addEventListener("click", async () => {
            const groupName = groupNameInput.value.trim();
            const selectedUsersData = Array.from(selectedUsers.querySelectorAll(".selected-user-item")).map(item => ({
                userKey: item.getAttribute("data-user-key"),
                userType: item.getAttribute("data-user-type"),
                userName: item.querySelector("span").textContent,
                userAvatar: item.querySelector("img").src
            }));
            console.log("Selected Users Data:", selectedUsersData);

            if (!groupName || !groupName.replace(/\s/g, '').length) {
                alert("Group name cannot be empty or contain only whitespace!");
                return;
            }
            if (!selectedAvatar || !/\.(jpe?g|png)$/i.test(selectedAvatar.name)) {
                alert("Please select a valid image file (.jpg or .png)!");
                return;
            }
            if (selectedUsersData.length === 0) {
                alert("Please select at least one member!");
                return;
            }

            const formData = new FormData();
            formData.append("groupName", groupName);
            formData.append("selectedAvatar", selectedAvatar);
            formData.append("users", JSON.stringify(selectedUsersData));

            try {
                const response = await fetch('/api/conversations/createGroup', {
                    method: 'POST',
                    body: formData
                });
                if (!response.ok) {
                    const errorText = await response.text();
                    console.error("Server response:", errorText);
                    throw new Error("Network response was not ok");
                }
                const result = await response.json();
                if (result.success) {
                    createGroupPopup.style.display = "none";
                    alert("Group created successfully!");
                    if (window.loadConversations && typeof window.loadConversations === 'function') {
                        await window.loadConversations();
                    }
                    if (window.connection && window.connection.state === signalR.HubConnectionState.Connected) {
                        await window.connection.invoke("NotifyGroupCreated", result.conversationKey, selectedUsersData.map(u => u.userKey));
                    }
                } else {
                    alert(`Failed to create group: ${result.message}`);
                }
            } catch (error) {
                console.error("Error creating group:", error);
                alert("An error occurred while creating the group!");
            }
        });
    }

    function debounce(func, wait) {
        let timeout;
        return function (...args) {
            clearTimeout(timeout);
            timeout = setTimeout(() => func.apply(this, args), wait);
        };
    }
})();