// ===== UTILITY FUNCTIONS =====
function debounce(func, wait) {
    let timeout;
    return function (...args) {
        clearTimeout(timeout);
        timeout = setTimeout(() => func.apply(this, args), wait);
    };
}

function getAvatarUrl(feedback) {
    const baseUrl = "https://localhost:7003";
    return feedback.AvatarUrl ? `${baseUrl}${feedback.AvatarUrl}` : "/images/avatar/default-avatar.jpg";
}

function getFeedbackTimeAgo(createdOn) {
    const now = new Date();
    const createdDate = new Date(createdOn);
    const diff = Math.floor((now - createdDate) / 60000);
    if (diff < 1) return "Just now";
    if (diff < 60) return `${diff} minute${diff > 1 ? 's' : ''} ago`;
    const hours = Math.floor(diff / 60);
    if (hours < 24) return `${hours} hour${hours > 1 ? 's' : ''} ago`;
    const days = Math.floor(hours / 24);
    return `${days} day${days > 1 ? 's' : ''} ago`;
}

// ===== NOTIFICATION MODAL HELPER =====
function showNotificationModal(type, title, message, onConfirm = null) {
    const $modal = $('#notificationModal');
    const $header = $('#notification-header');
    const $icon = $('.notification-icon');
    const $titleText = $('.notification-title-text');
    const $message = $('.notification-message');
    const $footer = $('#notification-footer');

    // Reset classes
    $header.removeClass('success error warning confirm');

    // Set type-specific styles
    let iconClass = '';
    switch (type) {
        case 'success':
            $header.addClass('success');
            iconClass = 'fas fa-check-circle';
            break;
        case 'error':
            $header.addClass('error');
            iconClass = 'fas fa-exclamation-circle';
            break;
        case 'warning':
            $header.addClass('warning');
            iconClass = 'fas fa-exclamation-triangle';
            break;
        case 'confirm':
            $header.addClass('confirm');
            iconClass = 'fas fa-question-circle';
            break;
    }

    // Set content
    $icon.attr('class', `notification-icon ${iconClass}`);
    $titleText.text(title);
    $message.text(message);

    // Set footer buttons
    $footer.empty();
    if (type === 'confirm') {
        $footer.append(`
            <button type="button" class="btn btn-cancel" data-bs-dismiss="modal">
                <i class="fas fa-times me-1"></i> Cancel
            </button>
            <button type="button" class="btn btn-confirm" id="notification-confirm-btn">
                <i class="fas fa-check me-1"></i> Confirm
            </button>
        `);

        // Bind confirm handler
        $('#notification-confirm-btn').off('click').on('click', function () {
            $modal.modal('hide');
            if (onConfirm) onConfirm();
        });
    } else {
        $footer.append(`
            <button type="button" class="btn btn-ok" data-bs-dismiss="modal">
                <i class="fas fa-check me-1"></i> OK
            </button>
        `);
    }

    // Show modal
    $modal.modal('show');
}

// ===== STATE MANAGEMENT =====
let feedbackState = {
    skip: 0,
    take: 50,
    isLoading: false,
    hasMore: true,
    isInitialLoad: false,
    currentReply: {
        feedbackKey: null,
        memberKey: null
    }
};

// ===== RENDER FUNCTIONS =====
function renderFeedback(fb) {
    const avatarUrl = getAvatarUrl(fb);
    const isResolved = fb.Status === 1;
    const isReplied = fb.Status === 2;

    let statusBadge = '';
    if (isReplied) {
        statusBadge = '<span class="status-badge replied">✓ Replied</span>';
    } else if (isResolved) {
        statusBadge = '<span class="status-badge resolved">✓ Resolved</span>';
    }

    const showActions = !isResolved && !isReplied;

    return `
        <div class="feedback-item" data-feedback-id="${fb.FeedbackKey}">
            <div class="d-flex align-items-start">
                <img src="${avatarUrl}" 
                     class="avatar" 
                     alt="${fb.Name}" 
                     onerror="this.src='/images/avatar/default-avatar.jpg';">
                <div class="content">
                    <div class="header-row">
                        <div class="name">${fb.Name}</div>
                        ${statusBadge}
                    </div>
                    <div class="text">${fb.Content}</div>
                    <div class="meta-info">
                        <span class="part-badge">Part ${fb.Part}</span>
                        <span class="time">${getFeedbackTimeAgo(fb.CreatedOn)}</span>
                    </div>
                    ${showActions ? `
                    <div class="actions">
                        <button class="btn btn-primary reply-feedback" 
                                data-feedback-key="${fb.FeedbackKey}" 
                                data-member-key="${fb.MemberKey}">
                            <i class="fas fa-reply me-1"></i> Reply
                        </button>
                        <button class="btn btn-secondary view-detail" 
                                data-feedback-key="${fb.FeedbackKey}">
                            <i class="fas fa-external-link-alt me-1"></i> View Question
                        </button>
                        <button class="btn btn-success mark-resolved" 
                                data-feedback-key="${fb.FeedbackKey}">
                            <i class="fas fa-check me-1"></i> Mark Resolved
                        </button>
                    </div>
                    ` : ''}
                </div>
            </div>
        </div>`;
}

// ===== LOAD FEEDBACKS =====
function loadFeedbacks(isPrepending = false) {
    if (feedbackState.isLoading || (!feedbackState.hasMore && isPrepending)) {
        return Promise.resolve();
    }

    feedbackState.isLoading = true;

    if (!isPrepending) {
        $("#loading-feedback").show();
    }

    const $list = $("#feedback-list");
    const $container = $("#feedback-container")[0];
    const oldScrollHeight = $container.scrollHeight;
    const shouldScrollToBottom = feedbackState.isInitialLoad;

    return $.getJSON(`/api/NotificationHandler/GetFeedbacks?skip=${feedbackState.skip}&take=${feedbackState.take}`)
        .done(data => {
            const items = data.feedbacks || [];

            if (items.length > 0) {
                items.reverse();
                const feedbacksHtml = items.map(renderFeedback).join('');

                if (isPrepending) {
                    $list.prepend(feedbacksHtml);
                    $container.scrollTop = $container.scrollHeight - oldScrollHeight;
                } else {
                    $list.append(feedbacksHtml);

                    if (shouldScrollToBottom) {
                        setTimeout(() => {
                            $container.scrollTop = $container.scrollHeight;
                            console.log('✅ Auto-scrolled to bottom:', $container.scrollTop);
                        }, 150);
                    }
                }
            }

            feedbackState.skip += items.length;
            feedbackState.hasMore = data.totalCount > feedbackState.skip;

            if ($list.children().length === 0) {
                $list.html(`
                    <div class="empty-state">
                        <p>No feedback available yet</p>
                    </div>
                `);
            }
        })
        .fail(xhr => {
            console.error("Error loading feedback:", xhr.status, xhr.responseText);
            if (!isPrepending) {
                $list.html(`
                    <div class="empty-state">
                        <p class="text-danger">Failed to load feedback. Please try again.</p>
                    </div>
                `);
            }
        })
        .always(() => {
            feedbackState.isLoading = false;
            feedbackState.isInitialLoad = false;
            $("#loading-feedback").hide();
        });
}

// ===== REPLY BOX FUNCTIONS =====
function openReplyBox(feedbackKey, memberKey) {
    feedbackState.currentReply = { feedbackKey, memberKey };
    $("#reply-box").addClass("active");
    $("#reply-text").val("").focus();
}

function closeReplyBox() {
    feedbackState.currentReply = { feedbackKey: null, memberKey: null };
    $("#reply-box").removeClass("active");
    $("#reply-text").val("");
}

function sendReplyFeedback() {
    const replyContent = $("#reply-text").val().trim();

    if (!replyContent) {
        showNotificationModal('warning', 'Empty Reply', 'Please enter your reply before sending.');
        return;
    }

    if (!feedbackState.currentReply.feedbackKey || !feedbackState.currentReply.memberKey) {
        showNotificationModal('error', 'Error', 'Unable to send reply. Please try again.');
        return;
    }

    const payload = {
        FeedbackKey: feedbackState.currentReply.feedbackKey,
        MemberKey: feedbackState.currentReply.memberKey,
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

                // ✅ Update UI immediately
                const $feedbackItem = $(`.feedback-item[data-feedback-id="${payload.FeedbackKey}"]`);
                $feedbackItem.find('.actions').remove();
                $feedbackItem.find('.header-row .status-badge').remove();
                $feedbackItem.find('.header-row').append('<span class="status-badge replied">✓ Replied</span>');

                // ✅ Show success modal (optional - silent success)
                showNotificationModal('success', 'Reply Sent', 'Your reply has been sent to the member successfully.');
            } else {
                showNotificationModal('error', 'Failed', res.message || 'Failed to send reply.');
            }
        },
        error: xhr => {
            console.error('Error sending reply:', xhr);
            showNotificationModal('error', 'Connection Error', 'Failed to send reply. Please check your connection and try again.');
        }
    });
}

// ===== MARK AS RESOLVED =====
function markFeedbackAsResolved(feedbackKey) {
    // ✅ Show confirmation modal instead of confirm()
    showNotificationModal(
        'confirm',
        'Confirm Action',
        'Are you sure you want to mark this feedback as resolved?',
        function () {
            // This runs only if user clicks Confirm
            $.ajax({
                url: '/api/NotificationHandler/MarkFeedbackAsResolved',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(feedbackKey),
                success: res => {
                    if (res.success) {
                        const $feedbackItem = $(`.feedback-item[data-feedback-id="${feedbackKey}"]`);
                        $feedbackItem.find('.actions').remove();
                        $feedbackItem.find('.header-row .status-badge').remove();
                        $feedbackItem.find('.header-row').append('<span class="status-badge resolved">✓ Resolved</span>');

                        // ✅ Optional success notification
                        showNotificationModal('success', 'Resolved', 'Feedback has been marked as resolved.');
                    } else {
                        showNotificationModal('error', 'Failed', res.message || 'Action failed.');
                    }
                },
                error: xhr => {
                    console.error('Error marking as resolved:', xhr);
                    showNotificationModal('error', 'Error', 'Action failed. Please try again.');
                }
            });
        }
    );
}

// ===== VIEW DETAIL =====
function viewDetail(feedbackKey) {
    $.getJSON(`/api/NotificationHandler/GetFeedbackDetail?feedbackKey=${feedbackKey}`)
        .done(data => {
            if (data.success) {
                const feedback = data.feedback;
                let url;

                if ([1, 2, 5].includes(feedback.Part)) {
                    url = `/TOEICPart${feedback.Part}/Question?Key=${feedback.QuestionKey}`;
                } else if ([3, 4, 6, 7].includes(feedback.Part)) {
                    url = `/ToeicPart${feedback.Part}/Question?Key=${feedback.QuestionKey}&source=QuestionSubList`;
                } else {
                    showNotificationModal('error', 'Invalid Data', 'Invalid Part number.');
                    return;
                }

                window.open(url, '_blank');
            } else {
                showNotificationModal('error', 'Not Found', data.message || "Could not retrieve feedback details.");
            }
        })
        .fail(xhr => {
            console.error("Error retrieving details:", xhr);
            showNotificationModal('error', 'Error', 'Error retrieving feedback details.');
        });
}

// ===== EVENT HANDLERS =====
function initFeedbackPopup() {
    const $container = $("#feedback-container");

    $container.on("scroll", debounce(function () {
        if ($(this).scrollTop() < 100 && feedbackState.hasMore && !feedbackState.isLoading && !feedbackState.isInitialLoad) {
            loadFeedbacks(true);
        }
    }, 300));

    $(document).on("click", ".mark-resolved", function () {
        markFeedbackAsResolved($(this).data("feedback-key"));
    });

    $(document).on("click", ".reply-feedback", function () {
        const feedbackKey = $(this).data("feedback-key");
        const memberKey = $(this).data("member-key");
        openReplyBox(feedbackKey, memberKey);
    });

    $(document).on("click", ".view-detail", function (e) {
        e.preventDefault();
        viewDetail($(this).data("feedback-key"));
    });

    $(document).on("click", ".btn-cancel-reply", closeReplyBox);
    $(document).on("click", ".btn-send-reply", sendReplyFeedback);

    $('#feedbackModal').on('show.bs.modal', function () {
        feedbackState = {
            skip: 0,
            take: 50,
            isLoading: false,
            hasMore: true,
            isInitialLoad: true,
            currentReply: {
                feedbackKey: null,
                memberKey: null
            }
        };

        $("#feedback-list").empty();
        loadFeedbacks(false);
    });

    $('#feedbackModal').on('hidden.bs.modal', closeReplyBox);
}

// ===== INITIALIZATION =====
$(document).ready(function () {
    initFeedbackPopup();
});