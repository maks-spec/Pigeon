let connection = null;
let currentUser = null;
let currentChat = null;

document.addEventListener('DOMContentLoaded', () => {
    currentUser = {
        id: sessionStorage.getItem('userId'),
        username: sessionStorage.getItem('username'),
        avatarUrl: sessionStorage.getItem('avatarUrl')
    };
    
    if (!currentUser.id) {
        window.location.href = '/Account/Login';
        return;
    }
    
    initializeSignalR();
    loadChats();
});

async function initializeSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl('/chathub')
        .build();
    
    connection.on('ReceiveMessage', (message) => {
        if (currentChat && message.chatId === currentChat.id) {
            displayMessage(message);
        }
    });
    
    await connection.start();
}

async function searchUser() {
    const phone = document.getElementById('searchInput').value;
    const resultDiv = document.getElementById('searchResult');
    
    try {
        const response = await fetch(`/api/users/search?phoneNumber=${encodeURIComponent(phone)}`);
        
        if (response.ok) {
            const user = await response.json();
            resultDiv.innerHTML = `
                <div class="user-search-result">
                    <img src="${user.avatarUrl || '/images/default-avatar.png'}" alt="Avatar" />
                    <div class="user-info">
                        <div>${user.username}</div>
                        <div class="phone">${user.phoneNumber}</div>
                    </div>
                    <button onclick="startChat(${user.id})">Написать</button>
                </div>
            `;
        } else {
            resultDiv.innerHTML = '<p class="not-found">Пользователь не найден</p>';
        }
    } catch (error) {
        resultDiv.innerHTML = '<p class="error">Ошибка поиска</p>';
    }
}

async function startChat(userId) {
    // Здесь логика создания чата
    currentChat = { id: Date.now(), userId };
    document.getElementById('chatArea').innerHTML = `
        <div class="chat-header">
            <h3>Чат с пользователем</h3>
        </div>
        <div class="messages" id="messages"></div>
        <div class="message-input">
            <input type="text" id="messageText" placeholder="Введите сообщение..." />
            <button onclick="sendMessage()">Отправить</button>
        </div>
    `;
    
    if (connection) {
        await connection.invoke('JoinChat', currentChat.id);
    }
}

async function sendMessage() {
    const messageText = document.getElementById('messageText').value;
    if (!messageText || !currentChat) return;
    
    await connection.invoke('SendMessage', currentChat.id, currentUser.id, messageText);
    document.getElementById('messageText').value = '';
}

function displayMessage(message) {
    const messagesDiv = document.getElementById('messages');
    const messageElement = document.createElement('div');
    messageElement.className = `message ${message.senderId == currentUser.id ? 'own' : 'other'}`;
    messageElement.innerHTML = `
        <div class="message-content">${message.content}</div>
        <div class="message-time">${new Date(message.sentAt).toLocaleTimeString()}</div>
    `;
    messagesDiv.appendChild(messageElement);
    messagesDiv.scrollTop = messagesDiv.scrollHeight;
}

function loadChats() {
    // Здесь загрузка списка чатов
}