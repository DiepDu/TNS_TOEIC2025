// Debounce function (Giữ nguyên)
function debounce(func, wait) {
    let timeout;
    return function (...args) {
        clearTimeout(timeout);
        timeout = setTimeout(() => func.apply(this, args), wait);
    };
}

// Variables
let feedbackSkip = 0; // Bắt đầu từ 0
const feedbackTake = 50; // Đã sửa thành 50
let isLoading = false;
let hasMoreFeedbacks = true;
let currentReplyFeedbackId = null;
let initialLoadDone = false; // Biến cờ để kiểm soát lần tải đầu tiên

// Helper: Get avatar URL (Giữ nguyên)
function getAvatarUrl(feedback) {
    const baseUrl = "https://localhost:7003"; // Cần điều chỉnh nếu baseUrl của bạn khác
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
                        <a class="btn btn-sm btn-secondary" href="/Question?key=${fb.QuestionKey}">Xem chi tiết</a>
                        <button class="btn btn-sm btn-success mark-resolved" data-feedback-id="${fb.FeedbackKey}" ${resolvedButtonStyle}>Đã xử lý</button>
                    </div>
                </div>
            </div>
        </div>`;
}

// Load feedbacks (Hàm này được sửa lại logic cuộn và cập nhật skip)
function loadFeedbacks(isPrepending = false) {
    // Nếu đang tải hoặc không còn feedback để tải VÀ đã tải lần đầu xong, thì thoát
    if (isLoading || (!hasMoreFeedbacks && initialLoadDone)) {
        console.log(`[Load] Aborted: isLoading=${isLoading}, hasMoreFeedbacks=${hasMoreFeedbacks}, initialLoadDone=${initialLoadDone}`);
        return;
    }
    isLoading = true;
    $("#loading-feedback").show();

    const $list = $("#feedback-list");
    const $container = $("#feedback-container")[0];

    // Lưu lại vị trí cuộn hiện tại và chiều cao nội dung trước khi tải thêm
    const oldScrollHeight = $container.scrollHeight;
    const oldScrollTop = $container.scrollTop;

    // feedbackSkip đã được định nghĩa là số lượng item đã load.
    // currentApiSkip chính là offset để lấy các tin cũ hơn nữa.
    const currentApiSkip = feedbackSkip;

    console.log(`[Load] Calling API with skip=${currentApiSkip}, take=${feedbackTake}, isPrepending=${isPrepending}`);

    $.getJSON(`/NotificationHandler/GetFeedbacks?skip=${currentApiSkip}&take=${feedbackTake}`)
        .done(data => {
            const items = data.feedbacks || [];
            console.log("[Load] API Response:", data);
            console.log(`[Load] Items received: ${items.length}, totalCount from DB: ${data.totalCount}`);

            if (!isPrepending) { // Lần tải đầu tiên (khi mở modal)
                $list.empty(); // Xóa sạch nội dung cũ
                // SẮP XẾP LẠI: Vì server trả về DESC, để hiển thị cũ nhất ở trên, mới nhất ở dưới, ta cần reverse()
                items.reverse().forEach(fb => $list.append(renderFeedback(fb))); // Thêm từng feedback vào cuối
                $container.scrollTop = $container.scrollHeight; // Cuộn xuống cuối cùng để thấy tin mới nhất
                initialLoadDone = true; // Đánh dấu đã tải lần đầu xong
                console.log("[Load] Initial load complete. Scrolled to bottom.");
            } else { // Tải thêm tin cũ hơn (khi cuộn lên)
                // Prepend từng feedback vào đầu danh sách (đúng thứ tự từ cũ đến mới trong batch)
                // Server đã trả về DESC (mới nhất của batch tiếp theo đến cũ nhất của batch đó),
                // vì vậy để prepend lên trên và giữ đúng thứ tự từ cũ đến mới, chúng ta cần reverse() trước khi prepend
                items.reverse().forEach(fb => $list.prepend(renderFeedback(fb)));

                // Điều chỉnh vị trí cuộn để giữ nguyên điểm nhìn
                const newScrollHeight = $container.scrollHeight;
                $container.scrollTop = oldScrollTop + (newScrollHeight - oldScrollHeight);
                console.log(`[Load] Prepended items. oldScrollHeight=${oldScrollHeight}, newScrollHeight=${newScrollHeight}, oldScrollTop=${oldScrollTop}, newScrollTop=${$container.scrollTop}`);
            }

            // Cập nhật feedbackSkip bằng số lượng feedback thực tế đã nhận được
            feedbackSkip += items.length;
            // hasMoreFeedbacks: true nếu tổng số feedback còn lại lớn hơn số lượng đã tải
            hasMoreFeedbacks = data.totalCount > feedbackSkip;
            console.log(`[Load] Updated feedbackSkip=${feedbackSkip}, hasMoreFeedbacks=${hasMoreFeedbacks}`);

            // Xử lý trường hợp không còn feedback nữa hoặc không có feedback nào
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
                    // Sau khi xóa, cần điều chỉnh lại feedbackSkip và hasMoreFeedbacks nếu item đó nằm trong số đã tải.
                    feedbackSkip--; // Giảm số lượng feedback đã tải nếu item bị xóa khỏi danh sách
                    if (feedbackSkip < 0) feedbackSkip = 0;
                    // Không cần đặt hasMoreFeedbacks = true ở đây, vì việc xóa 1 item
                    // không làm tăng số lượng item có thể tải thêm.
                    // Nếu cần tải lại để đảm bảo trạng thái, hãy gọi loadFeedbacks(false);
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

// Initialize popup
function initFeedbackPopup() {
    $("#feedback-container").on("scroll", debounce(function () {
        // Cuộn lên gần đầu (scrollTop < 50) VÀ còn dữ liệu để tải VÀ không đang tải VÀ đã tải lần đầu xong
        if ($(this).scrollTop() < 50 && hasMoreFeedbacks && !isLoading && initialLoadDone) {
            console.log("Scroll event: Triggering loadFeedbacks(true)");
            loadFeedbacks(true); // Tải thêm (prepend)
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

    $('#feedbackModal').on('show.bs.modal', function () {
        console.log("Modal show event: Resetting and loading initial feedbacks.");
        feedbackSkip = 0; // Reset skip về 0
        hasMoreFeedbacks = true; // Reset cờ này
        isLoading = false; // Đảm bảo reset isLoading
        initialLoadDone = false; // Quan trọng: Đặt lại cờ này để đảm bảo load mới từ đầu
        loadFeedbacks(false); // Tải lần đầu (không prepend)
    });

    $('#feedbackModal').on('hidden.bs.modal', closeReplyBox);
}

// Init
$(document).ready(function () {
    initFeedbackPopup();
});