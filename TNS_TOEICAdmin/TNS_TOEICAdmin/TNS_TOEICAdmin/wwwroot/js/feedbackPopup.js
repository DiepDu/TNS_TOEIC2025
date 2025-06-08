// Debounce function (Giữ nguyên)
function debounce(func, wait) {
    let timeout;
    return function (...args) {
        clearTimeout(timeout);
        timeout = setTimeout(() => func.apply(this, args), wait);
    };
}

// Variables (Giữ nguyên)
let feedbackSkip = 0;
const feedbackTake = 50;
let isLoading = false;
let hasMoreFeedbacks = true;
let currentReplyFeedbackId = null;
let initialLoadDone = false;

// Helper: Get avatar URL (Giữ nguyên)
function getAvatarUrl(feedback) {
    const baseUrl = "https://localhost:7003";
    return feedback.AvatarUrl ? `${baseUrl}${feedback.AvatarUrl}` : "/images/avatar/default-avatar.jpg";
}

// Helper: Convert datetime to time-ago (Giữ nguyên)
function getFeedbackTimeAgo(createdOn) {
    const now = new Date();
    const createdDate = new Date(createdOn);
    const diff = Math.floor((now - createdDate) / 60000);
    if (diff < 1) return "Vừa xong";
    if (diff < 60) return `${diff} phút trước`;
    const hours = Math.floor(diff / 60);
    if (hours < 24) return `${hours} giờ trước`;
    return `${Math.floor(hours / 24)} ngày trước`;
}

// Render feedback (Giữ nguyên)
function renderFeedback(fb) {
    const avatarUrl = getAvatarUrl(fb);
    const isResolved = fb.Status === 1;
    const resolvedButtonStyle = isResolved ? 'style="display: none;"' : '';
    return `
        <div class="feedback-item" data-feedback-id="${fb.FeedbackKey}">
            <div class="d-flex align-items-start">
                <img src="${avatarUrl}" class="avatar" onerror="this.src='/images/avatar/default-avatar.jpg';">
                <div class="content">
                    <div class="name">${fb.Name}</div>
                    <div class="text">${fb.Content}</div>
                    <div class="time">Part ${fb.Part} - ${getFeedbackTimeAgo(fb.CreatedOn)}</div>
                    <div class="actions">
                        <button class="btn btn-sm btn-primary reply-feedback" data-feedback-id="${fb.FeedbackKey}" data-member-key="${fb.MemberKey}">Trả lời</button>
                        <a class="btn btn-sm btn-secondary view-detail" data-feedback-id="${fb.FeedbackKey}" href="#">Xem chi tiết</a>
                        <button class="btn btn-sm btn-success mark-resolved" data-feedback-id="${fb.FeedbackKey}" ${resolvedButtonStyle}>Đã xử lý</button>
                    </div>
                </div>
            </div>
        </div>`;
}

// Load feedbacks (Giữ nguyên)
function loadFeedbacks(isPrepending = false) {
    if (isLoading || (!hasMoreFeedbacks && initialLoadDone)) {
        console.log(`[Load] Aborted: isLoading=${isLoading}, hasMoreFeedbacks=${hasMoreFeedbacks}, initialLoadDone=${initialLoadDone}`);
        return;
    }
    isLoading = true;
    $("#loading-feedback").show();

    const $list = $("#feedback-list");
    const $container = $("#feedback-container")[0];
    const oldScrollHeight = $container.scrollHeight;
    const oldScrollTop = $container.scrollTop;
    const currentApiSkip = feedbackSkip;

    console.log(`[Load] Calling API with skip=${currentApiSkip}, take=${feedbackTake}, isPrepending=${isPrepending}`);

    $.getJSON(`/NotificationHandler/GetFeedbacks?skip=${currentApiSkip}&take=${feedbackTake}`)
        .done(data => {
            const items = data.feedbacks || [];
            console.log("[Load] API Response:", data);
            console.log(`[Load] Items received: ${items.length}, totalCount from DB: ${data.totalCount}`);

            if (!isPrepending) {
                $list.empty();
                items.reverse().forEach(fb => $list.append(renderFeedback(fb)));
                $container.scrollTop = $container.scrollHeight;
                initialLoadDone = true;
                console.log("[Load] Initial load complete. Scrolled to bottom.");
            } else {
                items.reverse().forEach(fb => $list.prepend(renderFeedback(fb)));
                const newScrollHeight = $container.scrollHeight;
                $container.scrollTop = oldScrollTop + (newScrollHeight - oldScrollHeight);
                console.log(`[Load] Prepended items. oldScrollHeight=${oldScrollHeight}, newScrollHeight=${newScrollHeight}, oldScrollTop=${oldScrollTop}, newScrollTop=${$container.scrollTop}`);
            }

            feedbackSkip += items.length;
            hasMoreFeedbacks = data.totalCount > feedbackSkip;
            console.log(`[Load] Updated feedbackSkip=${feedbackSkip}, hasMoreFeedbacks=${hasMoreFeedbacks}`);

            if (!hasMoreFeedbacks && $list.children().length > 0) {
                console.log("[Load] All feedbacks loaded.");
            } else if (!hasMoreFeedbacks && $list.children().length === 0) {
                $list.html('<p class="text-center text-muted">Chưa có phản hồi nào.</p>');
                console.log("[Load] No feedbacks found.");
            }
        })
        .fail(xhr => {
            console.error("Lỗi khi tải phản hồi:", xhr.status, xhr.responseText);
            if (!isPrepending) {
                $("#feedback-list").html('<p class="text-center text-danger">Lỗi tải dữ liệu.</p>');
            }
        })
        .always(() => {
            isLoading = false;
            $("#loading-feedback").hide();
            console.log("[Load] Loading finished.");
        });
}

// Mark as resolved (Giữ nguyên)
function markFeedbackAsResolved(feedbackId) {
    $.ajax({
        url: '/NotificationHandler/MarkFeedbackAsResolved',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(feedbackId),
        success: res => {
            if (res.success) {
                $(`div[data-feedback-id="${feedbackId}"]`).fadeOut(300, function () {
                    $(this).remove();
                    feedbackSkip--;
                    if (feedbackSkip < 0) feedbackSkip = 0;
                });
            } else {
                alert(res.message || 'Xử lý thất bại.');
            }
        },
        error: xhr => {
            console.error('Lỗi xử lý:', xhr);
            alert('Xử lý thất bại.');
        }
    });
}

// Reply logic (Giữ nguyên)
function sendReplyFeedback(feedbackId, replyText) {
    $.ajax({
        url: '/NotificationHandler/SendReplyFeedback',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({ feedbackId: feedbackId, replyText: replyText }),
        success: res => {
            if (res.success) {
                closeReplyBox();
                alert('Phản hồi đã được gửi.');
            } else {
                alert(res.message || 'Gửi phản hồi thất bại.');
            }
        },
        error: xhr => {
            console.error('Lỗi gửi phản hồi:', xhr);
            alert('Gửi phản hồi thất bại.');
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

// Updated function to handle view detail with dynamic part
function viewDetail(feedbackId) {
    $.getJSON(`/NotificationHandler/GetFeedbackDetail?feedbackKey=${feedbackId}`)
        .done(data => {
            if (data.success) {
                const feedback = data.feedback;
                let url;
                if ([1, 2, 5].includes(feedback.Part)) {
                    url = `/TOEICPart${feedback.Part}/Question?Key=${feedback.QuestionKey}`;
                } else if ([3, 4, 6, 7].includes(feedback.Part)) {
                    url = `/ToeicPart${feedback.Part}/Question?Key=${feedback.QuestionKey}&source=QuestionSubList`;
                } else {
                    alert("Part không hợp lệ.");
                    return;
                }
                window.location.href = url; // Chuyển hướng đến URL tương ứng
            } else {
                alert(data.message || "Không thể lấy thông tin chi tiết.");
            }
        })
        .fail(xhr => {
            console.error("Lỗi khi lấy chi tiết:", xhr);
            alert("Lỗi khi lấy chi tiết.");
        });
}

// Initialize popup (Giữ nguyên, chỉ cập nhật event view-detail)
function initFeedbackPopup() {
    $("#feedback-container").on("scroll", debounce(function () {
        if ($(this).scrollTop() < 50 && hasMoreFeedbacks && !isLoading && initialLoadDone) {
            console.log("Scroll event: Triggering loadFeedbacks(true)");
            loadFeedbacks(true);
        }
    }, 200));

    $(document).on("click", ".mark-resolved", function () {
        markFeedbackAsResolved($(this).data("feedback-id"));
    });

    $(document).on("click", ".reply-feedback", function () {
        openReplyBox($(this).data("feedback-id"));
    });

    $(document).on("click", ".btn-cancel-reply", closeReplyBox);

    $(document).on("click", ".btn-send-reply", function () {
        const text = $("#reply-text").val().trim();
        if (!text) return alert("Vui lòng nhập nội dung phản hồi.");
        if (currentReplyFeedbackId) sendReplyFeedback(currentReplyFeedbackId, text);
    });

    $(document).on("click", ".view-detail", function (e) {
        e.preventDefault();
        const feedbackId = $(this).data("feedback-id");
        console.log("View detail clicked for FeedbackKey:", feedbackId);
        viewDetail(feedbackId);
    });

    $('#feedbackModal').on('show.bs.modal', function () {
        console.log("Modal show event: Resetting and loading initial feedbacks.");
        feedbackSkip = 0;
        hasMoreFeedbacks = true;
        isLoading = false;
        initialLoadDone = false;
        loadFeedbacks(false);
    });

    $('#feedbackModal').on('hidden.bs.modal', closeReplyBox);
}

// Init
$(document).ready(function () {
    initFeedbackPopup();
});