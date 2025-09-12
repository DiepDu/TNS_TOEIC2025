// File: wwwroot/js/chatbot_ai.js

document.addEventListener("DOMContentLoaded", () => {
    // === DOM ELEMENTS ===
    const chatbotToggler = document.getElementById("ai-chatbot-toggler");
    const chatbotPopup = document.querySelector(".ai-chatbot-popup");
    const closeChatbotBtn = document.getElementById("ai-close-chatbot");
    const chatBody = document.querySelector(".ai-chat-body");
    const messageInput = document.getElementById("ai-message-input");
    const chatForm = document.getElementById("ai-chat-form");
    const historyToggler = document.getElementById("ai-history-toggler");
    const conversationsList = document.querySelector(".ai-conversations-list");
    const newChatBtn = document.getElementById("ai-new-chat-btn");
    const fileUploadBtn = document.getElementById("ai-file-upload-btn");
    const fileInput = document.getElementById("ai-file-input");
    const screenAnalysisBtn = document.getElementById("ai-screen-analysis-btn");
    const filePreviewContainer = document.getElementById("ai-file-preview-container");

    // === STATE MANAGEMENT ===
    let currentConversationId = null;
    let attachedFiles = [];
    const MAX_FILES = 3;
    const MAX_IMAGE_SIZE = 10 * 1024 * 1024; // 10 MB
    const MAX_DOC_SIZE = 5 * 1024 * 1024;   // 5 MB
    window.isInitialDataLoaded = false;
    const initialInputHeight = messageInput.scrollHeight;
    let messagesLoadedCount = 0; // Đếm số tin nhắn đã tải
    let isLoadingMore = false;  
    // === CORE FUNCTIONS ===
    const createMessageElement = (content, sender) => {
        const messageDiv = document.createElement("div");
        messageDiv.classList.add("ai-message", `${sender}-message`);
        messageDiv.innerHTML = `<div class="ai-message-text">${content}</div>`;
        return messageDiv;
    };

    const showThinkingIndicator = () => {
        const thinkingDiv = createMessageElement(`<div class="thinking-indicator"><div class="dot"></div><div class="dot"></div><div class="dot"></div></div>`, 'bot');
        chatBody.appendChild(thinkingDiv);
        scrollToBottom();
        return thinkingDiv;
    };

    const scrollToBottom = () => {
        chatBody.scrollTop = chatBody.scrollHeight;
    };

    // === FILE HANDLING FUNCTIONS ===
    const fileToBase64 = (file) => {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.readAsDataURL(file);
            reader.onload = () => {
                const base64String = reader.result.split(',')[1];
                resolve({
                    fileName: file.name,
                    mimeType: file.type,
                    base64Data: base64String
                });
            };
            reader.onerror = (error) => reject(error);
        });
    };

    const handleFileSelection = (files) => {
        for (const file of files) {
            if (attachedFiles.length >= MAX_FILES) {
                Swal.fire('Limit Reached', `You can only attach up to ${MAX_FILES} files.`, 'warning');
                break;
            }
            if (validateFile(file)) {
                attachedFiles.push(file);
                createFilePreview(file);
            }
        }
        fileInput.value = null;
    };

    const validateFile = (file) => {
        const type = file.type;
        const size = file.size;
        if (type.startsWith("image/")) {
            if (size > MAX_IMAGE_SIZE) {
                Swal.fire('File Too Large', `"${file.name}" exceeds the ${MAX_IMAGE_SIZE / 1024 / 1024}MB limit.`, 'error');
                return false;
            }
        } else if (type === "application/pdf" || type.startsWith("text/") || type.includes("wordprocessingml")) {
            if (size > MAX_DOC_SIZE) {
                Swal.fire('File Too Large', `"${file.name}" exceeds the ${MAX_DOC_SIZE / 1024 / 1024}MB limit.`, 'error');
                return false;
            }
        } else {
            Swal.fire('Unsupported File', `The file type of "${file.name}" is not supported.`, 'warning');
            return false;
        }
        return true;
    };
    // File: wwwroot/js/chatbot_ai.js

    // === HÀM MỚI ĐỂ TẢI THÊM TIN NHẮN ===
    const loadMoreMessages = async () => {
        if (isLoadingMore || !currentConversationId) return; // Nếu đang tải hoặc không có conversationId thì không làm gì cả

        isLoadingMore = true;
        const loader = document.createElement('div');
        loader.className = 'ai-history-loader';
        chatBody.prepend(loader); // Thêm loader lên đầu

        try {
            const response = await fetch(`/api/ChatWithAI/GetMoreMessages?conversationId=${currentConversationId}&skipCount=${messagesLoadedCount}`);
            if (!response.ok) throw new Error("Failed to load more messages.");

            const olderMessages = await response.json();

            // Ghi lại chiều cao cũ của khung chat để giữ đúng vị trí cuộn
            const oldScrollHeight = chatBody.scrollHeight;

            if (olderMessages.length > 0) {
                olderMessages.forEach(msg => {
                    const sender = msg.SenderRole.toLowerCase() === 'user' ? 'user' : 'bot';
                    const messageElement = createMessageElement(msg.Content, sender);
                    chatBody.prepend(messageElement); // Thêm tin nhắn cũ lên đầu
                });
                messagesLoadedCount += olderMessages.length; // Cập nhật số lượng đã tải

                // Giữ nguyên vị trí cuộn sau khi thêm tin nhắn mới
                chatBody.scrollTop = chatBody.scrollHeight - oldScrollHeight;
            } else {
                // Không còn tin nhắn cũ để tải
                console.log("No more messages to load.");
            }
        } catch (error) {
            console.error("Error loading more messages:", error);
        } finally {
            loader.remove(); // Luôn xóa loader sau khi hoàn tất
            isLoadingMore = false;
        }
    };
    const createFilePreview = (file) => {
        const fileId = "file-" + Date.now() + Math.random();
        const previewWrapper = document.createElement("div");
        previewWrapper.className = "ai-file-preview-item";
        file.id = fileId;
        let previewContent = '';
        const fileURL = URL.createObjectURL(file);
        if (file.type.startsWith("image/")) {
            previewContent = `<img src="${fileURL}" alt="${file.name}" title="${file.name}">`;
        } else {
            previewContent = `<div class="file-icon">📄</div><div class="file-name" title="${file.name}">${file.name}</div>`;
        }
        previewWrapper.innerHTML = `<div class="preview-content">${previewContent}</div><button class="remove-file-btn" title="Remove file">✖</button>`;
        previewWrapper.querySelector('.remove-file-btn').addEventListener('click', () => {
            attachedFiles = attachedFiles.filter(f => f.id !== file.id);
            previewWrapper.remove();
            URL.revokeObjectURL(fileURL);
        });
        filePreviewContainer.appendChild(previewWrapper);
    };

    const fetchInitialData = async () => {
        chatBody.innerHTML = '<div class="ai-history-loader"></div>';
        try {
            const response = await fetch('/api/ChatWithAI/GetInitialData');
            if (!response.ok) throw new Error('Failed to load initial data.');
            const data = await response.json();
            chatBody.innerHTML = '';
            if (data.conversation) {
                currentConversationId = data.conversation.ConversationAIID;
                chatbotPopup.dataset.conversationId = data.conversation.ConversationAIID;

                // Vì backend trả về 50 tin nhắn mới nhất (thứ tự ngược),
                // chúng ta cần đảo ngược lại mảng để hiển thị đúng.
                data.messages.reverse();

                data.messages.forEach(msg => {
                    const sender = msg.SenderRole.toLowerCase() === 'user' ? 'user' : 'bot';
                    chatBody.appendChild(createMessageElement(msg.Content, sender));
                });
                // Khởi tạo bộ đếm tin nhắn đã tải
                messagesLoadedCount = data.messages.length;
            } else {
                chatbotPopup.dataset.conversationId = '';
                chatBody.appendChild(createMessageElement("Hello! I am Mr.TOEIC, your personal AI tutor. How can I help you today?", 'bot'));
                // Reset bộ đếm nếu không có cuộc hội thoại nào
                messagesLoadedCount = 0;
            }
            scrollToBottom();
        } catch (error) {
            console.error("Error fetching initial data:", error);
            chatBody.innerHTML = '';
            chatBody.appendChild(createMessageElement("Sorry, I couldn't connect to the server. Please try again later.", 'bot'));
        }
    };

    const fetchAllConversations = async () => {
        conversationsList.innerHTML = '<div class="ai-history-loader"></div>';
        try {
            const response = await fetch('/api/ChatWithAI/GetAllConversations');
            if (!response.ok) throw new Error('Failed to load conversations.');
            const conversations = await response.json();
            conversationsList.innerHTML = '';
            if (conversations.length === 0) {
                conversationsList.innerHTML = '<p style="text-align:center; color: #6c757d; padding: 20px;">No past conversations.</p>';
                return;
            }
            conversations.forEach(convo => {
                const convoDiv = document.createElement('div');
                convoDiv.classList.add('ai-conversation-item');
                convoDiv.dataset.id = convo.ConversationAIID;
                if (convo.ConversationAIID === currentConversationId) convoDiv.classList.add('active');
                convoDiv.innerHTML = `
                    <div class="ai-conversation-content"><div class="title">${convo.Title}</div><div class="last-message">${convo.LastMessage}</div></div>
                    <div class="rename-container"><input type="text" value="${convo.Title}" class="rename-input" /><button class="confirm-rename-btn">✔️</button><button class="cancel-rename-btn">✖️</button></div>
                    <div class="ai-conversation-actions"><button class="rename-btn" title="Rename">✏️</button><button class="delete-btn" title="Delete">🗑️</button></div>`;
                convoDiv.querySelector('.ai-conversation-content').addEventListener('click', () => handleConversationSelect(convo.ConversationAIID));
                convoDiv.querySelector('.rename-btn').addEventListener('click', (e) => { e.stopPropagation(); startRename(convoDiv); });
                convoDiv.querySelector('.delete-btn').addEventListener('click', (e) => { e.stopPropagation(); handleDeleteConversation(convo.ConversationAIID, convoDiv); });
                convoDiv.querySelector('.confirm-rename-btn').addEventListener('click', (e) => { e.stopPropagation(); confirmRename(convo.ConversationAIID, convoDiv); });
                convoDiv.querySelector('.cancel-rename-btn').addEventListener('click', (e) => { e.stopPropagation(); cancelRename(convoDiv); });
                conversationsList.appendChild(convoDiv);
            });
        } catch (error) {
            console.error("Error fetching conversations:", error);
            conversationsList.innerHTML = '<p style="text-align:center; color: #6c757d; padding: 20px;">Error loading history.</p>';
        }
    };

    const handleConversationSelect = async (conversationId) => {
        if (chatbotPopup.dataset.conversationId === conversationId) {
            chatbotPopup.classList.remove("show-history");
            return;
        }
        currentConversationId = conversationId;
        chatbotPopup.dataset.conversationId = conversationId;
        chatBody.innerHTML = '<div class="ai-history-loader"></div>';
        chatbotPopup.classList.remove("show-history");
        try {
            // GetMoreMessages mặc định tải 100 tin nhắn đầu tiên
            const response = await fetch(`/api/ChatWithAI/GetMoreMessages?conversationId=${conversationId}&skipCount=0`);
            if (!response.ok) throw new Error("Failed to load conversation.");
            const messages = await response.json();
            chatBody.innerHTML = '';
            messages.forEach(msg => {
                const sender = msg.SenderRole.toLowerCase() === 'user' ? 'user' : 'bot';
                chatBody.appendChild(createMessageElement(msg.Content, sender));
            });

            // Khởi tạo bộ đếm tin nhắn khi chọn một cuộc hội thoại khác
            messagesLoadedCount = messages.length;

            scrollToBottom();
            document.querySelectorAll('.ai-conversation-item').forEach(item => {
                item.classList.toggle('active', item.dataset.id === conversationId);
            });
        } catch (error) {
            console.error("Error loading conversation:", error);
            chatBody.innerHTML = '';
            chatBody.appendChild(createMessageElement("Sorry, couldn't load this conversation.", 'bot'));
        }
    };

    const startRename = (convoDiv) => { convoDiv.classList.add('renaming'); convoDiv.querySelector('.rename-input').focus(); };
    const cancelRename = (convoDiv) => { convoDiv.classList.remove('renaming'); };

    const confirmRename = async (conversationId, convoDiv) => {
        const input = convoDiv.querySelector('.rename-input');
        const newTitle = input.value.trim();
        if (!newTitle) { Swal.fire('Error', 'Title cannot be empty.', 'error'); return; }
        try {
            const response = await fetch('/api/ChatWithAI/RenameConversation', {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ conversationId, newTitle })
            });
            if (!response.ok) throw new Error('Failed to rename.');
            const result = await response.json();
            if (result.success) {
                convoDiv.querySelector('.title').textContent = newTitle;
                convoDiv.classList.remove('renaming');
            } else { throw new Error(result.message || 'API error'); }
        } catch (error) {
            console.error('Rename failed:', error);
            Swal.fire('Error', 'Could not rename the conversation.', 'error');
        }
    };
    const resetToNewChat = () => {
        currentConversationId = null;
        chatbotPopup.dataset.conversationId = '';
        attachedFiles = [];
        filePreviewContainer.innerHTML = '';
        chatBody.innerHTML = '';
        chatBody.appendChild(createMessageElement("New chat started! How can I assist you?", 'bot'));
        chatbotPopup.classList.remove("show-history");

        // Reset bộ đếm tin nhắn khi tạo chat mới
        messagesLoadedCount = 0;

        messageInput.focus();
    };
    const handleDeleteConversation = async (conversationId, convoDiv) => {
        Swal.fire({
            title: 'Are you sure?',
            text: "You won't be able to revert this!",
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Yes, delete it!'
        }).then(async (result) => {
            if (result.isConfirmed) {
                try {
                    const response = await fetch(`/api/ChatWithAI/DeleteConversation/${conversationId}`, { method: 'DELETE' });
                    if (!response.ok) throw new Error('Failed to delete.');

                    convoDiv.remove();

                    // SỬA LỖI TẠI ĐÂY
                    if (currentConversationId === conversationId) {
                        resetToNewChat(); // Gọi hàm trực tiếp thay vì click()
                    }

                    Swal.fire('Deleted!', 'Your conversation has been deleted.', 'success');
                } catch (error) {
                    console.error('Delete failed:', error);
                    Swal.fire('Error', 'Could not delete the conversation.', 'error');
                }
            }
        });
    };

    const sendMessage = async () => {
        const messageText = messageInput.value.trim();
        if (!messageText && attachedFiles.length === 0) return;

        let userMessageHTML = messageText;
        if (attachedFiles.length > 0) {
            userMessageHTML += `<br><small style='opacity: 0.8;'><i>(${attachedFiles.length} file(s) attached)</i></small>`;
        }
        chatBody.appendChild(createMessageElement(userMessageHTML, 'user'));

        messageInput.value = '';
        messageInput.style.height = `${initialInputHeight}px`;
        scrollToBottom();

        const thinkingIndicator = showThinkingIndicator();
        let conversationIdForRequest = chatbotPopup.dataset.conversationId;

        try {
            if (!conversationIdForRequest) {
                const createResponse = await fetch('/api/ChatWithAI/CreateNewConversation', { method: 'POST' });
                if (!createResponse.ok) throw new Error("Server could not create a new conversation.");
                const createData = await createResponse.json();
                if (createData.success && createData.conversationId) {
                    conversationIdForRequest = createData.conversationId;
                    currentConversationId = conversationIdForRequest;
                    chatbotPopup.dataset.conversationId = conversationIdForRequest;
                } else {
                    throw new Error(createData.message || "API failed to return a valid conversation ID.");
                }
            }

            const filePayloads = await Promise.all(attachedFiles.map(file => fileToBase64(file)));

            const payload = {
                ConversationId: conversationIdForRequest,
                Message: messageText,
                Files: filePayloads
                // Đã xóa ScreenData
            };

            const response = await fetch('/api/ChatWithAI/HandleMemberChat', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });

            attachedFiles = [];
            filePreviewContainer.innerHTML = '';

            if (!response.ok) {
                const errorData = await response.json().catch(() => ({ message: `Server responded with status: ${response.status}` }));
                throw new Error(errorData.message);
            }

            const result = await response.json();
            thinkingIndicator.remove();
            if (result.success) {
                chatBody.appendChild(createMessageElement(result.message, 'bot'));
            } else {
                throw new Error(result.message || "An unknown error occurred.");
            }
        } catch (error) {
            console.error("Send message error:", error);
            thinkingIndicator.remove();
            chatBody.appendChild(createMessageElement(`Error: ${error.message}`, 'bot'));
        }
        scrollToBottom();
    };

    chatBody.addEventListener('scroll', () => {
        // Nếu người dùng cuộn lên đến đỉnh
        if (chatBody.scrollTop === 0 && !isLoadingMore) {
            loadMoreMessages();
        }
    });

    // === EVENT LISTENERS ===
    chatbotToggler.addEventListener("click", () => {
        const isOpening = !document.body.classList.contains("show-ai-chatbot");
        document.body.classList.toggle("show-ai-chatbot");
        if (isOpening && !window.isInitialDataLoaded) {
            fetchInitialData();
            window.isInitialDataLoaded = true;
        }
    });
    closeChatbotBtn.addEventListener("click", () => document.body.classList.remove("show-ai-chatbot"));
    historyToggler.addEventListener("click", () => {
        chatbotPopup.classList.toggle("show-history");
        if (chatbotPopup.classList.contains("show-history")) { fetchAllConversations(); }
    });
    newChatBtn.addEventListener("click", () => {
        resetToNewChat();
    });
    chatForm.addEventListener("submit", (e) => { e.preventDefault(); sendMessage(); });
    messageInput.addEventListener("input", () => {
        messageInput.style.height = 'auto';
        messageInput.style.height = `${messageInput.scrollHeight}px`;
    });
    messageInput.addEventListener('keydown', (e) => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); sendMessage(); } });
    fileUploadBtn.addEventListener("click", () => fileInput.click());
    fileInput.addEventListener("change", (e) => { if (e.target.files.length > 0) { handleFileSelection(e.target.files); } });
 
});