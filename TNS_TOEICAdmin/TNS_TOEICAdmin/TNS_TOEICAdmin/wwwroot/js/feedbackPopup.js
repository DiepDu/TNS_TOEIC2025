// TÌM VÀ THAY THẾ TOÀN BỘ FILE NÀY

function debounce(func, wait) {
    let timeout;
    return function (...args) {
        clearTimeout(timeout);
        timeout = setTimeout(() => func.apply(this, args), wait);
    };
}

let feedbackSkip = 0;
const feedbackTake = 50;
let isLoading = false;
let hasMoreFeedbacks = true;
let currentReplyInfo = {
    feedbackKey: null,
    memberKey: null
};
let isInitialLoading = false; // Cờ để tránh xung đột cuộn

function getAvatarUrl(feedback) {
    const baseUrl = "https://localhost:7003";
    return feedback.AvatarUrl ? `${baseUrl}${feedback.AvatarUrl}` : "/images/avatar/default-avatar.jpg";
}

function getFeedbackTimeAgo(createdOn) {
    const now = new Date();
    const createdDate = new Date(createdOn);
    const diff = Math.floor((now - createdDate) / 60000);
    if (diff < 1) return "Just now";
    if (diff < 60) return `${diff} minutes ago`;
    const hours = Math.floor(diff / 60);
    if (hours < 24) return `${hours} hours ago`;
    return `${Math.floor(hours / 24)} days ago`;
}

function renderFeedback(fb) {
    const avatarUrl = getAvatarUrl(fb);
    const isResolved = fb.Status === 1;
    const isReplied = fb.Status === 2;
    let statusBadge = '';
    if (isReplied) {
        statusBadge = '<span class="badge bg-info ms-2">Replied</span>';
    } else if (isResolved) {
        statusBadge = '<span class="badge bg-success ms-2">Resolved</span>';
    }
    const actionsVisible = !isResolved && !isReplied;
    return `
        <div class="feedback-item" data-feedback-id="${fb.FeedbackKey}">
            <div class="d-flex align-items-start">
                <img src="${avatarUrl}" class="avatar" onerror="this.src='/images/avatar/default-avatar.jpg';">
                <div class="content">
                    <div class="name">${fb.Name} ${statusBadge}</div>
                    <div class="text">${fb.Content}</div>
                    <div class="time">Part ${fb.Part} - ${getFeedbackTimeAgo(fb.CreatedOn)}</div>
                    ${actionsVisible ? `
                    <div class="actions">
                        <button class="btn btn-sm btn-primary reply-feedback" data-feedback-key="${fb.FeedbackKey}" data-member-key="${fb.MemberKey}">Reply</button>
                        <a class="btn btn-sm btn-secondary view-detail" data-feedback-key="${fb.FeedbackKey}" href="#">View Details</a>
                        <button class="btn btn-sm btn-success mark-resolved" data-feedback-key="${fb.FeedbackKey}">Mark as Resolved</button>
                    </div>
                    ` : ''}
                </div>
            </div>
        </div>`;
}

function loadFeedbacks(isPrepending = false) {
    if (isLoading || (!hasMoreFeedbacks && isPrepending)) {
        return $.Deferred().resolve().promise();
    }
    isLoading = true;
    if (!isPrepending) { // Chỉ hiện spinner to ở giữa khi tải lần đầu
        $("#loading-feedback").show();
    }

    const $list = $("#feedback-list");
    const $container = $("#feedback-container")[0];
    const oldScrollHeight = $container.scrollHeight;

    $.getJSON(`/api/NotificationHandler/GetFeedbacks?skip=${feedbackSkip}&take=${feedbackTake}`)
        .done(data => {
            const items = data.feedbacks || [];
            if (items.length > 0) {
                items.reverse();
                const feedbacksHtml = items.map(renderFeedback).join('');
                const $feedbacksHtml = $(feedbacksHtml);
                const $images = $feedbacksHtml.find('img.avatar');
                const promises = $images.map(function () {
                    const d = $.Deferred();
                    $(this).on('load', d.resolve).on('error', d.resolve);
                    if (this.complete) d.resolve();
                    return d.promise();
                }).get();

                $.when.apply($, promises).done(function () {
                    if (isPrepending) {
                        $list.prepend($feedbacksHtml);
                        $container.scrollTop = $container.scrollHeight - oldScrollHeight;
                    } else {
                        $list.append($feedbacksHtml);
                        $container.scrollTop = $container.scrollHeight;
                    }
                });
            }

            feedbackSkip += items.length;
            hasMoreFeedbacks = data.totalCount > feedbackSkip;
            if ($list.children().length === 0) {
                $list.html('<p class="text-center text-muted">No feedback yet.</p>');
            }
        })
        .fail(xhr => {
            console.error("Error loading feedback:", xhr.status, xhr.responseText);
            if (!isPrepending) {
                $list.html('<p class="text-center text-danger">Error loading data.</p>');
            }
        })
        .always(() => {
            isLoading = false;
            isInitialLoading = false; // Tắt cờ sau lần tải đầu tiên
            $("#loading-feedback").hide();
        });
}

function sendReplyFeedback() {
    const replyContent = $("#reply-text").val().trim();
    if (!replyContent) {
        alert("Please enter reply content.");
        return;
    }
    if (!currentReplyInfo.feedbackKey || !currentReplyInfo.memberKey) {
        alert("Error: Could not find feedback information to reply to.");
        return;
    }
    const payload = {
        FeedbackKey: currentReplyInfo.feedbackKey,
        MemberKey: currentReplyInfo.memberKey,
        ReplyContent: replyContent
    };
    $.ajax({
        url: '/api/NotificationHandler/SendFeedbackReply',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(payload),
        success: res => {
            if (res.success) {
                closeReplyBox();
                alert('Reply has been sent.');
                const $feedbackItem = $(`div[data-feedback-id="${payload.FeedbackKey}"]`);
                $feedbackItem.find('.actions').remove();
                $feedbackItem.find('.name').append('<span class="badge bg-info ms-2">Replied</span>');
            } else {
                alert(res.message || 'Failed to send reply.');
            }
        },
        error: xhr => {
            console.error('Error sending reply:', xhr);
            alert('Failed to send reply. Please check the console for details.');
        }
    });
}

function markFeedbackAsResolved(feedbackId) {
    $.ajax({
        url: '/api/NotificationHandler/MarkFeedbackAsResolved',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(feedbackId),
        success: res => {
            if (res.success) {
                const $feedbackItem = $(`div[data-feedback-id="${feedbackId}"]`);
                $feedbackItem.find('.actions').remove();
                $feedbackItem.find('.name').append('<span class="badge bg-success ms-2">Resolved</span>');
            } else {
                alert(res.message || 'Action failed.');
            }
        },
        error: xhr => {
            console.error('Error marking as resolved:', xhr);
            alert('Action failed.');
        }
    });
}

function openReplyBox(feedbackKey, memberKey) {
    currentReplyInfo = { feedbackKey, memberKey };
    $("#reply-box").addClass("active");
    $("#reply-text").val("").focus();
}

function closeReplyBox() {
    currentReplyInfo = { feedbackKey: null, memberKey: null };
    $("#reply-box").removeClass("active");
    $("#reply-text").val("");
}

function viewDetail(feedbackId) {
    $.getJSON(`/api/NotificationHandler/GetFeedbackDetail?feedbackKey=${feedbackId}`)
        .done(data => {
            if (data.success) {
                const feedback = data.feedback;
                let url;
                if ([1, 2, 5].includes(feedback.Part)) {
                    url = `/TOEICPart${feedback.Part}/Question?Key=${feedback.QuestionKey}`;
                } else if ([3, 4, 6, 7].includes(feedback.Part)) {
                    url = `/ToeicPart${feedback.Part}/Question?Key=${feedback.QuestionKey}&source=QuestionSubList`;
                } else {
                    alert("Invalid Part.");
                    return;
                }
                window.open(url, '_blank');
            } else {
                alert(data.message || "Could not retrieve details.");
            }
        })
        .fail(xhr => {
            console.error("Error retrieving details:", xhr);
            alert("Error retrieving details.");
        });
}

function initFeedbackPopup() {
    $("#feedback-container").on("scroll", debounce(function () {
        if ($(this).scrollTop() < 50 && hasMoreFeedbacks && !isLoading && !isInitialLoading) {
            loadFeedbacks(true);
        }
    }, 200));

    // --- SỬA LỖI CÁC NÚT BẤM ---
    $(document).on("click", ".mark-resolved", function () {
        // Sửa lại cách lấy data attribute cho đúng
        markFeedbackAsResolved($(this).data("feedback-key"));
    });

    $(document).on("click", ".reply-feedback", function () {
        const feedbackKey = $(this).data("feedback-key");
        const memberKey = $(this).data("member-key");
        openReplyBox(feedbackKey, memberKey);
    });

    $(document).on("click", ".view-detail", function (e) {
        e.preventDefault();
        // Sửa lại cách lấy data attribute cho đúng
        viewDetail($(this).data("feedback-key"));
    });
    // --- KẾT THÚC SỬA LỖI CÁC NÚT BẤM ---


    $(document).on("click", ".btn-cancel-reply", closeReplyBox);
    $(document).on("click", ".btn-send-reply", sendReplyFeedback);

    $('#feedbackModal').on('show.bs.modal', function () {
        isInitialLoading = true; // Bật cờ
        feedbackSkip = 0;
        hasMoreFeedbacks = true;
        isLoading = false;
        $("#feedback-list").empty();
        loadFeedbacks(false);
    });

    $('#feedbackModal').on('hidden.bs.modal', closeReplyBox);
}

$(document).ready(function () {
    initFeedbackPopup();
});