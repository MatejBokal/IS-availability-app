// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Notification dropdown functionality
document.addEventListener('DOMContentLoaded', function() {
    const notificationDropdown = document.getElementById('notificationDropdown');
    if (notificationDropdown) {
        // Load notification count on page load
        updateNotificationCount();
        
        // Load notifications when dropdown is opened
        notificationDropdown.addEventListener('shown.bs.dropdown', function() {
            loadRecentNotifications();
        });
    }
});

function updateNotificationCount() {
    fetch('/api/notifications/unread-count')
        .then(response => response.json())
        .then(data => {
            const badge = document.getElementById('notificationBadge');
            if (badge) {
                if (data.count > 0) {
                    badge.textContent = data.count > 99 ? '99+' : data.count;
                    badge.style.display = 'inline';
                } else {
                    badge.style.display = 'none';
                }
            }
        })
        .catch(error => console.error('Error loading notification count:', error));
}

function loadRecentNotifications() {
    const notificationList = document.getElementById('notificationList');
    if (!notificationList) return;

    fetch('/api/notifications/recent')
        .then(response => response.json())
        .then(data => {
            if (data.length === 0) {
                notificationList.innerHTML = '<div class="dropdown-item-text text-muted text-center">Nimate obvestil</div>';
                return;
            }

            let html = '';
            data.forEach(notif => {
                const date = new Date(notif.createdAtUtc);
                const dateStr = date.toLocaleDateString('sl-SI', { day: '2-digit', month: '2-digit', year: 'numeric' }) + ' ' + 
                               date.toLocaleTimeString('sl-SI', { hour: '2-digit', minute: '2-digit' });
                
                html += `
                    <li>
                        <div class="dropdown-item-text ${!notif.isRead ? 'fw-bold' : ''}">
                            <div class="d-flex justify-content-between align-items-start">
                                <div class="flex-grow-1">
                                    <div class="small">${notif.title}</div>
                                    <div class="text-muted" style="font-size: 0.85rem;">${notif.body}</div>
                                    <div class="text-muted" style="font-size: 0.75rem;">${dateStr}</div>
                                </div>
                                ${!notif.isRead ? '<span class="badge bg-primary ms-2">NOVO</span>' : ''}
                            </div>
                        </div>
                    </li>
                `;
            });
            
            notificationList.innerHTML = html;
        })
        .catch(error => {
            console.error('Error loading notifications:', error);
            notificationList.innerHTML = '<div class="dropdown-item-text text-danger text-center">Napaka pri nalaganju obvestil</div>';
        });
}