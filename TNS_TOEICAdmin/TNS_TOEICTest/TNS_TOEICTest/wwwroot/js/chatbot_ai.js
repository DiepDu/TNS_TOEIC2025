document.addEventListener("DOMContentLoaded", () => {
    // === DOM ELEMENTS ===
    const chatbotToggler = document.getElementById("ai-chatbot-toggler");
    const chatbotPopup = document.querySelector(".ai-chatbot-popup");
    const closeChatbotBtn = document.getElementById("ai-close-chatbot");
    const chatBody = document.querySelector(".ai-chat-body");
    const messageInput = document.getElementById("ai-message-input");
    const sendBtn = document.getElementById("ai-send-message-btn");
    const chatForm = document.getElementById("ai-chat-form");
    const historyToggler = document.getElementById("ai-history-toggler");
    const conversationsList = document.querySelector(".ai-conversations-list");
    const newChatBtn = document.getElementById("ai-new-chat-btn");

    // File & Screen Analysis buttons
    const fileUploadBtn = document.getElementById("ai-file-upload-btn");
    const fileInput = document.getElementById("ai-file-input");
    const screenAnalysisBtn = document.getElementById("ai-screen-analysis-btn");

    // === STATE MANAGEMENT ===
    let currentConversationId = null;
    let isScreenAnalysisOn = false;
    window.isInitialDataLoaded = false;
    const initialInputHeight = messageInput.scrollHeight;

    // === CORE FUNCTIONS ===

    // Tạo một phần tử tin nhắn
    const createMessageElement = (content, sender) => {
        const messageDiv = document.createElement("div");
        messageDiv.classList.add("ai-message", `${sender}-message`);
        // Note: For security, in a real app, sanitize HTML content from the bot.
        messageDiv.innerHTML = `<div class="ai-message-text">${content}</div>`;
        return messageDiv;
    };

    // Hiển thị chỉ báo "đang suy nghĩ"
    const showThinkingIndicator = () => {
        const thinkingDiv = createMessageElement(
            `<div class="thinking-indicator"><div class="dot"></div><div class="dot"></div><div class="dot"></div></div>`,
            'bot'
        );
        chatBody.appendChild(thinkingDiv);
        scrollToBottom();
        return thinkingDiv;
    };

    // Cuộn xuống cuối khung chat
    const scrollToBottom = () => {
        chatBody.scrollTop = chatBody.scrollHeight;
    };

    // === API CALLS ===

    // 1. Tải dữ liệu ban đầu (cuộc trò chuyện gần nhất)
    const fetchInitialData = async () => {
        chatBody.innerHTML = '<div class="ai-history-loader"></div>';
        try {
            const response = await fetch('/api/ChatWithAI/GetInitialData');
            if (!response.ok) throw new Error('Failed to load initial data.');

            const data = await response.json();
            chatBody.innerHTML = ''; // Xóa loader

            if (data.conversation) {
                currentConversationId = data.conversation.conversationAIID;
                data.messages.forEach(msg => {
                    const sender = msg.senderRole.toLowerCase() === 'user' ? 'user' : 'bot';
                    chatBody.appendChild(createMessageElement(msg.content, sender));
                });
            } else {
                // Trường hợp người dùng mới, chưa có cuộc trò chuyện nào
                chatBody.appendChild(createMessageElement("Hello! I am Mr.TOEIC, your personal AI tutor. How can I help you today?", 'bot'));
            }
            scrollToBottom();
        } catch (error) {
            console.error("Error fetching initial data:", error);
            chatBody.innerHTML = '';
            chatBody.appendChild(createMessageElement("Sorry, I couldn't connect to the server. Please try again later.", 'bot'));
        }
    };

    // 2. Tải danh sách tất cả cuộc hội thoại
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
                convoDiv.dataset.id = convo.conversationAIID;
                if (convo.conversationAIID === currentConversationId) {
                    convoDiv.classList.add('active');
                }
                convoDiv.innerHTML = `
                    <div class="title">${convo.title}</div>
                    <div class="last-message">${convo.lastMessage}</div>
                `;
                convoDiv.addEventListener('click', () => handleConversationSelect(convo.conversationAIID));
                conversationsList.appendChild(convoDiv);
            });
        } catch (error) {
            console.error("Error fetching conversations:", error);
            conversationsList.innerHTML = '<p style="text-align:center; color: #6c757d; padding: 20px;">Error loading history.</p>';
        }
    };

    // 3. Gửi tin nhắn
    const sendMessage = async () => {
        const messageText = messageInput.value.trim();
        if (!messageText) return;

        // Hiển thị tin nhắn người dùng ngay lập tức
        chatBody.appendChild(createMessageElement(messageText, 'user'));
        messageInput.value = '';
        messageInput.style.height = `${initialInputHeight}px`;
        scrollToBottom();

        const thinkingIndicator = showThinkingIndicator();

        let conversationIdForRequest = currentConversationId;

        try {
            // Nếu là cuộc hội thoại mới, tạo nó trước
            if (!conversationIdForRequest) {
                const createResponse = await fetch('/api/ChatWithAI/CreateNewConversation', { method: 'POST' });
                if (!createResponse.ok) throw new Error("Failed to create new conversation.");
                const createData = await createResponse.json();
                if (createData.success) {
                    currentConversationId = createData.conversationId;
                    conversationIdForRequest = currentConversationId;
                } else {
                    throw new Error("API failed to create new conversation.");
                }
            }

            // Lấy dữ liệu màn hình nếu nút được bật
            let screenData = null;
            if (isScreenAnalysisOn) {
                // *** LOGIC LẤY DỮ LIỆU MÀN HÌNH SẼ ĐƯỢC THÊM VÀO ĐÂY ***
                // Ví dụ:
                // const scoreElement = document.querySelector("#test-score");
                // if(scoreElement) screenData = `User is viewing a page with test score: ${scoreElement.innerText}`;
                screenData = "Screen analysis is ON. (Placeholder - add specific data scraping logic here)";
            }

            // Gửi tin nhắn đến API xử lý chính
            const response = await fetch('/api/ChatWithAI/HandleMemberChat', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    conversationId: conversationIdForRequest,
                    message: messageText,
                    screenData: screenData
                })
            });

            if (!response.ok) throw new Error('Message sending failed.');

            const result = await response.json();

            // Xóa chỉ báo "đang nghĩ" và hiển thị tin nhắn của bot
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

    // === EVENT HANDLERS ===

    // Xử lý khi chọn một cuộc hội thoại từ sidebar
    const handleConversationSelect = async (conversationId) => {
        if (currentConversationId === conversationId) {
            chatbotPopup.classList.remove("show-history");
            return;
        }
        currentConversationId = conversationId;
        chatBody.innerHTML = '<div class="ai-history-loader"></div>';
        chatbotPopup.classList.remove("show-history");

        try {
            // Tải 100 tin nhắn đầu tiên của cuộc hội thoại này
            const response = await fetch(`/api/ChatWithAI/GetMoreMessages?conversationId=${conversationId}&skipCount=0`);
            if (!response.ok) throw new Error("Failed to load conversation.");

            const messages = await response.json();
            chatBody.innerHTML = '';
            messages.forEach(msg => {
                const sender = msg.senderRole.toLowerCase() === 'user' ? 'user' : 'bot';
                chatBody.appendChild(createMessageElement(msg.content, sender));
            });
            scrollToBottom();

            // Cập nhật trạng thái active cho item trong sidebar
            document.querySelectorAll('.ai-conversation-item').forEach(item => {
                item.classList.toggle('active', item.dataset.id === conversationId);
            });

        } catch (error) {
            console.error("Error loading conversation:", error);
            chatBody.innerHTML = '';
            chatBody.appendChild(createMessageElement("Sorry, couldn't load this conversation.", 'bot'));
        }
    };

    // Mở/đóng chatbot
    chatbotToggler.addEventListener("click", () => {
        const isOpening = !document.body.classList.contains("show-ai-chatbot");
        document.body.classList.toggle("show-ai-chatbot");

        // SỬA LỖI TẠI ĐÂY:
        // Luôn gọi fetchInitialData khi mở chatbot lần đầu tiên trong một phiên truy cập.
        // Biến isInitialDataLoaded sẽ ngăn việc gọi lại không cần thiết.
        if (isOpening && !window.isInitialDataLoaded) {
            fetchInitialData();
            window.isInitialDataLoaded = true; // Đánh dấu là đã tải lần đầu
        }
    });
    closeChatbotBtn.addEventListener("click", () => document.body.classList.remove("show-ai-chatbot"));

    // Mở/đóng sidebar lịch sử
    historyToggler.addEventListener("click", () => {
        chatbotPopup.classList.toggle("show-history");
        if (chatbotPopup.classList.contains("show-history")) {
            fetchAllConversations();
        }
    });

    // Bắt đầu cuộc trò chuyện mới
    newChatBtn.addEventListener("click", () => {
        currentConversationId = null;
        chatBody.innerHTML = '';
        chatBody.appendChild(createMessageElement("New chat started! How can I assist you?", 'bot'));
        chatbotPopup.classList.remove("show-history");
        messageInput.focus();
    });

    // Gửi tin nhắn
    chatForm.addEventListener("submit", (e) => {
        e.preventDefault();
        sendMessage();
    });

    // Tự động điều chỉnh chiều cao ô input
    messageInput.addEventListener("input", () => {
        messageInput.style.height = `${initialInputHeight}px`;
        messageInput.style.height = `${messageInput.scrollHeight}px`;
    });

    // Nút tải file
    fileUploadBtn.addEventListener("click", () => fileInput.click());
    fileInput.addEventListener("change", (e) => {
        if (e.target.files.length > 0) {
            const fileName = e.target.files[0].name;
            Swal.fire({
                title: 'File Attached!',
                text: `${fileName} will be sent with your next message.`,
                icon: 'success',
                toast: true,
                position: 'top-end',
                showConfirmButton: false,
                timer: 3000,
                timerProgressBar: true,
            });
            // Logic xử lý file sẽ nằm trong hàm sendMessage
        }
    });

    // Nút phân tích màn hình
    screenAnalysisBtn.addEventListener("click", () => {
        isScreenAnalysisOn = !isScreenAnalysisOn;
        screenAnalysisBtn.classList.toggle("active", isScreenAnalysisOn);
        Swal.fire({
            title: `Screen Analysis ${isScreenAnalysisOn ? 'ON' : 'OFF'}`,
            text: isScreenAnalysisOn ? 'I will now analyze the content on your screen with your next message.' : 'I will no longer analyze your screen content.',
            icon: 'info',
            toast: true,
            position: 'top-end',
            showConfirmButton: false,
            timer: 3000,
            timerProgressBar: true,
        });
    });

    // === INITIALIZATION ===
    // Không tải gì cả cho đến khi người dùng click mở chatbot
});
