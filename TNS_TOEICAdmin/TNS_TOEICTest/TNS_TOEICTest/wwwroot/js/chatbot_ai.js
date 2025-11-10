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
    const filePreviewContainer = document.getElementById("ai-file-preview-container");

    // === STATE MANAGEMENT ===
    let currentConversationId = null;
    let attachedFiles = [];
    const MAX_FILES = 3;
    const MAX_IMAGE_SIZE = 10 * 1024 * 1024; // 10 MB
    const MAX_DOC_SIZE = 5 * 1024 * 1024;   // 5 MB
    window.isInitialDataLoaded = false;
    const initialInputHeight = messageInput.scrollHeight;
    let messagesLoadedCount = 0;
    let isLoadingMore = false;

    const createMessageElement = (content, sender, timestamp = null) => {
        const messageDiv = document.createElement("div");
        messageDiv.classList.add("ai-message", `${sender}-message`);

        if (timestamp) {
            messageDiv.dataset.timestamp = timestamp;
        } else {
            messageDiv.dataset.timestamp = Date.now();
        }

        const safeContent = content || "";
        let contentHtml = safeContent;

        if (sender === 'bot') {
            // ✅ STEP 1: Decode HTML entities CHỈ 1 LẦN
            const textarea = document.createElement('textarea');
            textarea.innerHTML = safeContent;
            let decodedContent = textarea.value;

            // ✅ STEP 2: Parse markdown (breaks: true để xử lý \n)
            marked.setOptions({
                sanitize: false,
                breaks: true,  // ✅ Convert \n to <br>
                gfm: true,
                headerIds: false,
                mangle: false
            });

            contentHtml = marked.parse(decodedContent);
        }

        // ✅ Insert HTML
        messageDiv.innerHTML = `<div class="ai-message-text">${contentHtml}</div>`;

        // ✅ STEP 3: Force-render media (UNCHANGED)
        if (sender === 'bot') {
            const messageTextDiv = messageDiv.querySelector('.ai-message-text');

            // Process images
            const images = messageTextDiv.querySelectorAll('img');
           
            images.forEach((img, index) => {
              
                img.style.cssText = 'display:block !important; max-width:350px; max-height:350px; object-fit:contain; border-radius:12px; margin:10px 0; cursor:pointer; border:2px solid #e0e0e0; box-shadow:0 4px 12px rgba(0,0,0,0.1);';

                img.addEventListener('click', () => {
                    Swal.fire({
                        imageUrl: img.src,
                        imageAlt: 'Image preview',
                        showCloseButton: true,
                        showConfirmButton: false,
                        width: 'auto',
                        background: 'transparent',
                        backdrop: 'rgba(0,0,0,0.9)'
                    });
                });

                img.addEventListener('error', (e) => {
                  
                    img.alt = '❌ Image failed to load';
                    img.style.border = '2px solid #dc3545';
                });

                img.addEventListener('load', () => {
                   
                });
            });

            // Process audio
            const audioElements = messageTextDiv.querySelectorAll('audio');
           
            audioElements.forEach((audio, index) => {
              
                audio.style.cssText = 'display:block !important; width:100%; max-width:400px; margin:12px 0; border-radius:8px; height:40px; background:#f0f0f0; border:1px solid #e0e0e0;';
                audio.controls = true;
                audio.preload = 'metadata';

                audio.addEventListener('error', (e) => {
                  
                    const errorMsg = document.createElement('div');
                    errorMsg.textContent = '❌ Audio failed to load';
                    errorMsg.style.cssText = 'color:#dc3545; font-size:0.9rem; margin:8px 0;';
                    audio.parentNode.insertBefore(errorMsg, audio);
                });

                audio.addEventListener('loadedmetadata', () => {
                   
                });
            });

            // Syntax highlighting
            const codeBlocks = messageTextDiv.querySelectorAll('pre code');
            codeBlocks.forEach((block) => {
                try {
                    hljs.highlightElement(block);
                } catch (e) {
                    console.warn('[Syntax Highlighting Failed]:', e);
                }
            });
        }

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

    const sortMessagesByTimestamp = () => {
        const messages = Array.from(chatBody.querySelectorAll('.ai-message'));
        if (messages.length === 0) return;

        messages.sort((a, b) => {
            const timeA = parseInt(a.dataset.timestamp) || 0;
            const timeB = parseInt(b.dataset.timestamp) || 0;
            return timeA - timeB;
        });

        chatBody.innerHTML = '';
        messages.forEach(msg => chatBody.appendChild(msg));
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

    const loadMoreMessages = async () => {
        if (isLoadingMore || !currentConversationId) return;

        isLoadingMore = true;
        const loader = document.createElement('div');
        loader.className = 'ai-history-loader';
        chatBody.prepend(loader);

        try {
            const response = await fetch(`/api/ChatWithAI/GetMoreMessages?conversationId=${currentConversationId}&skipCount=${messagesLoadedCount}`);
            if (!response.ok) throw new Error("Failed to load more messages.");

            const olderMessages = await response.json();
            const oldScrollHeight = chatBody.scrollHeight;

            if (olderMessages.length > 0) {
                // ✅ Backend returns oldest first, prepend in reverse
                for (let i = olderMessages.length - 1; i >= 0; i--) {
                    const msg = olderMessages[i];
                    const sender = msg.SenderRole.toLowerCase() === 'user' ? 'user' : 'bot';
                    const timestamp = new Date(msg.Timestamp).getTime();
                    chatBody.prepend(createMessageElement(msg.Content, sender, timestamp));
                }

                messagesLoadedCount += olderMessages.length;
                sortMessagesByTimestamp();
                chatBody.scrollTop = chatBody.scrollHeight - oldScrollHeight;
            }
        } catch (error) {
            console.error("Error loading more messages:", error);
        } finally {
            loader.remove();
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

                data.messages.forEach(msg => {
                    const sender = msg.SenderRole.toLowerCase() === 'user' ? 'user' : 'bot';
                    const timestamp = new Date(msg.Timestamp).getTime();
                    chatBody.appendChild(createMessageElement(msg.Content, sender, timestamp));
                });

                sortMessagesByTimestamp();
                messagesLoadedCount = data.messages.length;
            } else {
                chatbotPopup.dataset.conversationId = '';
                chatBody.appendChild(createMessageElement("Hello! I am Mr.TOEIC, your personal AI tutor. How can I help you today?", 'bot'));
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

                let cleanLastMessage = convo.LastMessage || "No messages yet";
                cleanLastMessage = cleanLastMessage
                    .replace(/!\[.*?\]\(.*?\)/g, '🖼️')
                    .replace(/\[.*?\]\(.*?\)/g, '')
                    .replace(/[*_~`]/g, '')
                    .replace(/<audio.*?<\/audio>/g, '🎵')
                    .replace(/<[^>]+>/g, '')
                    .replace(/\s+/g, ' ')
                    .trim();

                if (cleanLastMessage.length > 60) {
                    cleanLastMessage = cleanLastMessage.substring(0, 60) + '...';
                }

                convoDiv.innerHTML = `
                <div class="ai-conversation-content">
                    <div class="title">${convo.Title}</div>
                    <div class="last-message">${cleanLastMessage}</div>
                </div>
                <div class="rename-container">
                    <input type="text" value="${convo.Title}" class="rename-input" />
                    <button class="confirm-rename-btn">✔️</button>
                    <button class="cancel-rename-btn">✖️</button>
                </div>
                <div class="ai-conversation-actions">
                    <button class="rename-btn" title="Rename">✏️</button>
                    <button class="delete-btn" title="Delete">🗑️</button>
                </div>`;

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
            const response = await fetch(`/api/ChatWithAI/GetMoreMessages?conversationId=${conversationId}&skipCount=0`);
            if (!response.ok) throw new Error("Failed to load conversation.");

            const messages = await response.json();
            chatBody.innerHTML = '';

            messages.forEach(msg => {
                const sender = msg.SenderRole.toLowerCase() === 'user' ? 'user' : 'bot';
                const timestamp = new Date(msg.Timestamp).getTime();
                chatBody.appendChild(createMessageElement(msg.Content, sender, timestamp));
            });

            sortMessagesByTimestamp();
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
                    if (currentConversationId === conversationId) {
                        resetToNewChat();
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

        // ✅ Add user message with current timestamp
        const userTimestamp = Date.now();
        const userMsg = createMessageElement(userMessageHTML, 'user', userTimestamp);
        chatBody.appendChild(userMsg);

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
                // ✅ Add bot message with timestamp slightly after user message
                const botTimestamp = userTimestamp + 1;
                const botMsg = createMessageElement(result.message, 'bot', botTimestamp);
                chatBody.appendChild(botMsg);

                // ✅ Sort to ensure correct order
                sortMessagesByTimestamp();
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