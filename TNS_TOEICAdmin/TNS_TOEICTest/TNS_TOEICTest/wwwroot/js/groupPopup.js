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
                if (selectedAvatar) {
                    avatarPreview.src = URL.createObjectURL(selectedAvatar);
                    avatarPreview.classList.remove("d-none");
                }
            });
            fileInput.click();
        });
    }

    function loadInitialUsers() {
        userList.innerHTML = `
            <div class="user-item" data-user-key="user1">
                <img src="/images/avatar/default-avatar.jpg" alt="User">
                <span>User 1</span>
            </div>
            <div class="user-item" data-user-key="user2">
                <img src="/images/avatar/default-avatar.jpg" alt="User">
                <span>User 2</span>
            </div>
            <div class="user-item" data-user-key="user3">
                <img src="/images/avatar/default-avatar.jpg" alt="User">
                <span>User 3</span>
            </div>
        `;
        document.querySelectorAll(".user-item").forEach(item => {
            item.addEventListener("click", () => {
                const userKey = item.getAttribute("data-user-key");
                const userName = item.querySelector("span").textContent;
                const userAvatar = item.querySelector("img").src;
                if (!selectedUsers.querySelector(`[data-user-key="${userKey}"]`)) {
                    selectedUsers.innerHTML += `
                        <div class="selected-user-item" data-user-key="${userKey}">
                            <img src="${userAvatar}" alt="Selected User">
                            <span>${userName}</span>
                            <span class="remove-btn">x</span>
                        </div>
                    `;
                    item.remove();
                }
                attachRemoveListeners();
            });
        });
    }

    function attachRemoveListeners() {
        document.querySelectorAll(".remove-btn").forEach(btn => {
            btn.addEventListener("click", (e) => {
                const userItem = e.target.parentElement;
                const userKey = userItem.getAttribute("data-user-key");
                const userName = userItem.querySelector("span").textContent;
                const userAvatar = userItem.querySelector("img").src;
                userList.innerHTML += `
                    <div class="user-item" data-user-key="${userKey}">
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
                const userName = item.querySelector("span").textContent;
                const userAvatar = item.querySelector("img").src;
                if (!selectedUsers.querySelector(`[data-user-key="${userKey}"]`)) {
                    selectedUsers.innerHTML += `
                        <div class="selected-user-item" data-user-key="${userKey}">
                            <img src="${userAvatar}" alt="Selected User">
                            <span>${userName}</span>
                            <span class="remove-btn">x</span>
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
            const query = e.target.value.trim();
            if (query) {
                const filtered = Array.from(userList.querySelectorAll(".user-item")).filter(item =>
                    item.querySelector("span").textContent.toLowerCase().includes(query.toLowerCase())
                );
                userList.innerHTML = filtered.map(item => item.outerHTML).join("");
                attachUserClickListeners();
            } else {
                loadInitialUsers();
            }
        }, 300));
    }

    if (createGroupBtn) {
        createGroupBtn.addEventListener("click", () => {
            const groupName = groupNameInput.value.trim();
            const selectedUsersData = Array.from(selectedUsers.querySelectorAll(".selected-user-item")).map(item => ({
                userKey: item.getAttribute("data-user-key"),
                userName: item.querySelector("span").textContent,
                userAvatar: item.querySelector("img").src
            }));
            if (groupName && selectedUsersData.length > 0) {
                console.log("Group Name:", groupName);
                console.log("Selected Users:", selectedUsersData);
                console.log("Avatar:", selectedAvatar);
                createGroupPopup.style.display = "none";
            } else {
                alert("Vui lòng nhập tên nhóm và chọn ít nhất một thành viên.");
            }
        });
    }

    // Hàm debounce để tối ưu hóa tìm kiếm
    function debounce(func, wait) {
        let timeout;
        return function (...args) {
            clearTimeout(timeout);
            timeout = setTimeout(() => func.apply(this, args), wait);
        };
    }
})();