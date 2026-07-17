self.addEventListener('push', (event) => {
    const data = event.data ? event.data.json() : {};
    event.waitUntil(
        self.registration.showNotification(data.title || 'Reminder', {
            body: data.body || '',
            icon: '/icon.png',
            data: { reminderId: data.reminderId }
        })
    );
});

self.addEventListener('notificationclick', (event) => {
    event.notification.close();
    const reminderId = event.notification.data?.reminderId;

    if (reminderId) {
        fetch('https://customerexcelapi-production.up.railway.app/api/reminders/' + reminderId + '/read', {
            method: 'PATCH',
            headers: { 'X-User-Id': '550e8400-e29b-41d4-a716-446655440000' }
        });
    }

    event.waitUntil(clients.openWindow('/'));
});
