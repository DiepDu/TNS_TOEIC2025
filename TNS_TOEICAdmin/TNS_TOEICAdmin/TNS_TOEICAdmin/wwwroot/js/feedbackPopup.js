// Định nghĩa debounce
function debounce(func, wait) {
    let timeout;
    return function (...args) {
        clearTimeout(timeout);
        timeout = setTimeout(() => func.apply(this, args), wait);
    };
}

// Khởi tạo biến
let feedbackSkip = 0;
const feedbackTake = 50;
let isLoading = false;
let hasMoreFeedbacks = true;
let currentReplyFeedbackId = null;

function getAvatarUrl(feedback) {
    const baseUrl = "https://localhost:7003"; // Domain của dự án Test
    return feedback.AvatarUrl ? `${baseUrl}${feedback.AvatarUrl}` : "/images/avatar/default-avatar.jpg";
}

function getFeedbackTimeAgo(createdOn) {
    const now = new Date();
    const createdDate = new Date(createdOn);
    const diffInMinutes = Math.floor((now - createdDate) / (1000 * 60));
    if (diffInMinutes < 1) return "Vừa xong";
    if (diffInMinutes < 60) return `${diffInMinutes} phút trước`;
    const diffInHours = Math.floor(diffInMinutes / 60);
    if (diffInHours < 24) return `${diffInHours} giờ trước`;
    const diffInDays = Math.floor(diffInHours / 24);
    return `${diffInDays} ngày trước`;
}

function loadFeedbacks(append = false) {
    if (isLoading || !hasMoreFeedbacks) return;
    isLoading = true;
    $("#loading-feedback").show();

    const url = '/NotificationHandler/GetFeedbacks';
    console.log('Calling GetFeedbacks:', url, { skip: feedbackSkip, take: feedbackTake });

    $.ajax({
        url: `${url}?skip=${feedbackSkip}&take=${feedbackTake}`,
        type: 'GET',
        dataType: 'json',
        success: function (data) {
            console.log('GetFeedbacks response:', data);
            const $list = $("#feedback-list");
            if (!append) $list.empty();

            if (data.feedbacks && data.feedbacks.length > 0) {
                data.feedbacks.forEach(function (feedback) {
                    const avatarUrl = getAvatarUrl(feedback);
                    const isResolved = feedback.Status === 1;
                    const resolvedButtonStyle = isResolved ? 'style="display: none;"' : '';
                    const feedbackHtml = `
                        <div class="feedback-item" data-feedback-id="${feedback.FeedbackKey}">
                            <div class="d-flex align-items-start">
                                <img src="${avatarUrl}" alt="Avatar" class="avatar" onerror="this.src='/images/avatar/default-avatar.jpg';">
                                <div class="content">
                                    <div class="name">${feedback.Name}</div>
                                    <div class="text">${feedback.Content}</div>
                                    <div class="time">Part ${feedback.Part} - ${getFeedbackTimeAgo(feedback.CreatedOn)}</div>
                                    <div class="actions">
                                        <button class="btn btn-sm btn-primary reply-feedback" data-feedback-id="${feedback.FeedbackKey}" data-member-key="${feedback.MemberKey}">Trả lời</button>
                                        <a class="btn btn-sm btn-secondary" href="/Question?key=${feedback.QuestionKey}">Xem chi tiết</a>
                                        <button class="btn btn-sm btn-success mark-resolved" data-feedback-id="${feedback.FeedbackKey}" ${resolvedButtonStyle}>Đã xử lý</button>
                                    </div>
                                </div>
                            </div>
                        </div>`;
                    $list.append(feedbackHtml);
                });
                feedbackSkip += data.count;
                hasMoreFeedbacks = data.totalCount > feedbackSkip;
                if (!append) {
                    const container = $("#feedback-container")[0];
                    container.scrollTop = container.scrollHeight;
                }
            } else {
                hasMoreFeedbacks = false;
                if ($list.is(":empty")) {
                    $list.html('<p class="text-center text-muted">No feedbacks available.</p>');
                }
            }
            isLoading = false;
            $("#loading-feedback").hide();
        },
        error: function (xhr) {
            console.error('Error fetching feedbacks:', xhr.status, xhr.statusText, xhr.responseText);
            $("#feedback-list").html('<p class="text-center text-danger">Error loading feedbacks.</p>');
            isLoading = false;
            $("#loading-feedback").hide();
        }
    });
}

function markFeedbackAsResolved(feedbackId) {
    const url = '/NotificationHandler/MarkFeedbackAsResolved';
    console.log('Calling MarkFeedbackAsResolved:', url, { feedbackId });

    $.ajax({
        url: url,
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(feedbackId),
        beforeSend: function (xhr) {
            xhr.setRequestHeader('X-CSRF-TOKEN', $('meta[name="csrf-token"]').attr('content') || '');
        },
        success: function (response) {
            console.log('MarkFeedbackAsResolved response:', response);
            if (response.success) {
                $(`div[data-feedback-id="${feedbackId}"] .mark-resolved`).hide();
            } else {
                alert(response.message || 'Failed to mark feedback as resolved.');
            }
        },
        error: function (xhr) {
            console.error('Error marking feedback:', xhr.status, xhr.statusText, xhr.responseText);
            alert('Failed to mark feedback as resolved.');
        }
    });
}

function sendReplyFeedback(feedbackId, replyText) {
    const url = '/NotificationHandler/SendReplyFeedback';
    console.log('Calling SendReplyFeedback:', url, { feedbackId, replyText });

    $.ajax({
        url: url,
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({ feedbackId, replyText }),
        success: function (response) {
            console.log('SendReplyFeedback response:', response);
            if (response.success) {
                closeReplyBox();
                alert('Phản hồi đã được gửi.');
            } else {
                alert(response.message || 'Failed to send reply.');
            }
        },
        error: function (xhr) {
            console.error('Error sending reply:', xhr.status, xhr.statusText, xhr.responseText);
            alert('Failed to send reply.');
        }
    });
}

function openReplyBox(feedbackId) {
    currentReplyFeedbackId = feedbackId;
    $("#reply-box").addClass("active");
    $("#reply-text").val("").focus();
}

function closeReplyBox() {
    currentReplyFeedbackId = null;
    $("#reply-box").removeClass("active");
    $("#reply-text").val("");
}

// Gắn sự kiện
function initFeedbackPopup() {
    try {
        console.log('Initializing feedback popup events.');
        $("#feedback-container").on("scroll", debounce(function () {
            const scrollTop = $(this).scrollTop();
            if (scrollTop < 50 && hasMoreFeedbacks && !isLoading) {
                loadFeedbacks(true);
            }
        }, 200));

        $(document).on("click", ".mark-resolved", function () {
            const feedbackId = $(this).data("feedback-id");
            console.log('Mark resolved clicked:', feedbackId);
            markFeedbackAsResolved(feedbackId);
        });

        $(document).on("click", ".reply-feedback", function () {
            const feedbackId = $(this).data("feedback-id");
            console.log('Reply clicked:', feedbackId);
            openReplyBox(feedbackId);
        });

        $(document).on("click", ".btn-cancel-reply", function () {
            closeReplyBox();
        });

        $(document).on("click", ".btn-send-reply", function () {
            const replyText = $("#reply-text").val().trim();
            if (!replyText) {
                alert("Vui lòng nhập nội dung phản hồi.");
                return;
            }
            if (currentReplyFeedbackId) {
                sendReplyFeedback(currentReplyFeedbackId, replyText);
            }
        });

        $('#feedbackModal').on('show.bs.modal', function () {
            console.log('Feedback modal opened.');
            feedbackSkip = 0;
            hasMoreFeedbacks = true;
            loadFeedbacks();
        });

        $('#feedbackModal').on('hidden.bs.modal', function () {
            closeReplyBox();
        });
    } catch (e) {
        console.error("Error initializing feedback popup:", e);
    }
}

// Khởi tạo
initFeedbackPopup();