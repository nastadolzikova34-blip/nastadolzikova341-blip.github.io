using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace WatchTogether.Hubs
{
    public class VideoHub : Hub
    {
        // Храним информацию о пользователях (ConnectionId -> (UserName, Avatar))
        private static readonly ConcurrentDictionary<string, UserInfo> Users = new();

        // Храним информацию о комнатах (RoomId -> List<ConnectionId>)
        private static readonly ConcurrentDictionary<string, List<string>> Rooms = new();

        // Храним статус голосовой активности пользователей
        private static readonly ConcurrentDictionary<string, DateTime> SpeakingUsers = new();

        // Класс для хранения информации о пользователе
        public class UserInfo
        {
            public string UserName { get; set; } = string.Empty;
            public string? Avatar { get; set; }
            public bool IsGuest { get; set; }
        }

        // Присоединение к комнате (старая версия для обратной совместимости)
        public async Task JoinRoom(string roomId, string userName)
        {
            await JoinRoomWithProfile(roomId, userName, null, true);
        }

        // Присоединение к комнате с профилем
        public async Task JoinRoomWithProfile(string roomId, string userName, string? avatar, bool isGuest = true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

            // Сохраняем информацию о пользователе
            Users[Context.ConnectionId] = new UserInfo
            {
                UserName = userName,
                Avatar = avatar,
                IsGuest = isGuest
            };

            // Добавляем в список комнаты
            Rooms.AddOrUpdate(roomId,
                new List<string> { Context.ConnectionId },
                (key, existingList) =>
                {
                    if (!existingList.Contains(Context.ConnectionId))
                    {
                        existingList.Add(Context.ConnectionId);
                    }
                    return existingList;
                });

            // Уведомляем всех в комнате о новом участнике (с аватаркой)
            await Clients.Group(roomId).SendAsync("UserJoinedWithProfile",
                Context.ConnectionId, userName, avatar);

            // Отправляем новому пользователю список всех участников
            if (Rooms.TryGetValue(roomId, out var users))
            {
                foreach (var userId in users)
                {
                    if (userId != Context.ConnectionId && Users.TryGetValue(userId, out var userInfo))
                    {
                        await Clients.Client(Context.ConnectionId).SendAsync("UserJoinedWithProfile",
                            userId, userInfo.UserName, userInfo.Avatar);
                    }
                }
            }

            // Системное сообщение в чат
            await SendMessage(roomId, "System", $"✨ {userName} присоединился к комнате");

            Console.WriteLine($"Пользователь {userName} ({Context.ConnectionId}) присоединился к комнате {roomId}");
        }

        // Отправка сообщения в чат
        public async Task SendMessage(string roomId, string username, string message)
        {
            Console.WriteLine($"Сообщение в комнате {roomId} от {username}: {message}");

            // Отправляем сообщение всем в комнате
            await Clients.Group(roomId).SendAsync("ReceiveMessage",
                Context.ConnectionId, username, message, DateTime.UtcNow);
        }

        // Отправка URL видео
        public async Task SendVideoUrl(string roomId, string url, string videoType)
        {
            await Clients.Group(roomId).SendAsync("ReceiveVideoUrl",
                Context.ConnectionId, url, videoType);
            Console.WriteLine($"Комната {roomId}: загружено видео {videoType}");
        }

        // Пауза для всех
        public async Task SendPause(string roomId)
        {
            await Clients.OthersInGroup(roomId).SendAsync("ReceivePause", Context.ConnectionId);
            Console.WriteLine($"Комната {roomId}: отправлена команда паузы");
        }

        // Воспроизведение для всех
        public async Task SendPlay(string roomId, double currentTime)
        {
            await Clients.OthersInGroup(roomId).SendAsync("ReceivePlay", Context.ConnectionId, currentTime);
            Console.WriteLine($"Комната {roomId}: отправлена команда воспроизведения на {currentTime} сек");
        }

        //  НОВЫЕ МЕТОДЫ 

        /// <summary>
        /// Отправка живой реакции (эмодзи) поверх видео
        /// </summary>
        public async Task SendReaction(string roomId, string emoji, object position)
        {
            Console.WriteLine($"Комната {roomId}: реакция {emoji} от {Context.ConnectionId}");
            await Clients.Group(roomId).SendAsync("ReceiveReaction", Context.ConnectionId, emoji, position);
        }

        /// <summary>
        /// Обновление статуса голосовой активности пользователя
        /// </summary>
        public async Task UpdateSpeakingStatus(string roomId, bool isSpeaking)
        {
            var userId = Context.ConnectionId;

            if (isSpeaking)
            {
                SpeakingUsers[userId] = DateTime.UtcNow;
            }
            else
            {
                SpeakingUsers.TryRemove(userId, out _);
            }

            // Рассылаем статус всем в комнате
            await Clients.Group(roomId).SendAsync("UserSpeaking", userId, isSpeaking);

            // Проверяем, сколько пользователей сейчас говорят одновременно
            var activeSpeakers = SpeakingUsers
                .Where(kvp => (DateTime.UtcNow - kvp.Value).TotalSeconds < 2)
                .Select(kvp => kvp.Key)
                .ToList();

            Console.WriteLine($"Комната {roomId}: активных говорящих: {activeSpeakers.Count}");

            // Если 2 и более пользователей говорят одновременно — запускаем конфетти
            if (activeSpeakers.Count >= 2 && isSpeaking)
            {
                await TriggerLaughConfetti(roomId);
            }
        }

        /// <summary>
        /// Запуск конфетти для всех в комнате (смех)
        /// </summary>
        public async Task TriggerLaughConfetti(string roomId)
        {
            Console.WriteLine($"Комната {roomId}: запуск конфетти (коллективный смех)");
            await Clients.Group(roomId).SendAsync("TriggerConfetti");

            // Отправляем системное сообщение в чат
            await SendMessage(roomId, "System", "🎉 Все смеются! Конфетти-вечеринка!");
        }

        /// <summary>
        /// Бросок кубика для выбора "главного по вечеру"
        /// </summary>
        public async Task RollDice(string roomId, List<string> users)
        {
            if (users == null || users.Count == 0)
            {
                Console.WriteLine($"Комната {roomId}: попытка броска кубика без участников");
                return;
            }

            var random = new Random();
            var diceResult = random.Next(1, 7); // 1-6
            var chosenIndex = random.Next(users.Count);
            var chosenUser = users[chosenIndex];

            Console.WriteLine($"Комната {roomId}: бросок кубика. Результат: {diceResult}, выбран: {chosenUser}");

            // Рассылаем результат всем в комнате
            await Clients.Group(roomId).SendAsync("ReceiveDiceRoll", Context.ConnectionId, diceResult, chosenUser);

            // Отправляем системное сообщение
            await SendMessage(roomId, "System", $"🎲 Кубик брошен! Выпало: {diceResult}. 👑 {chosenUser} сегодня выбирает видео!");
        }

        /// <summary>
        /// Получить список активных говорящих пользователей (для диагностики)
        /// </summary>
        public List<string> GetActiveSpeakers()
        {
            return SpeakingUsers
                .Where(kvp => (DateTime.UtcNow - kvp.Value).TotalSeconds < 2)
                .Select(kvp => kvp.Key)
                .ToList();
        }

        // Обновление профиля пользователя
        public async Task UpdateProfile(string userName, string? avatar)
        {
            if (Users.TryGetValue(Context.ConnectionId, out var userInfo))
            {
                // Обновляем информацию
                userInfo.UserName = userName;
                userInfo.Avatar = avatar;

                // Находим все комнаты пользователя
                foreach (var room in Rooms)
                {
                    if (room.Value.Contains(Context.ConnectionId))
                    {
                        // Уведомляем всех в комнате об обновлении профиля
                        await Clients.Group(room.Key).SendAsync("UserProfileUpdated",
                            Context.ConnectionId, userName, avatar);
                    }
                }

                Console.WriteLine($"Профиль обновлен для {userName}");
            }
        }

        // Получить список пользователей в комнате
        public List<string> GetUsersInRoom(string roomId)
        {
            return Rooms.TryGetValue(roomId, out var users) ? users : new List<string>();
        }

        // Получить информацию о пользователе
        public UserInfo? GetUserInfo(string connectionId)
        {
            return Users.TryGetValue(connectionId, out var info) ? info : null;
        }

        // Покидание комнаты
        public async Task LeaveRoom(string roomId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);

            if (Users.TryGetValue(Context.ConnectionId, out var userInfo))
            {
                if (Rooms.TryGetValue(roomId, out var users))
                {
                    users.Remove(Context.ConnectionId);

                    // Уведомляем остальных
                    await Clients.Group(roomId).SendAsync("UserLeft",
                        Context.ConnectionId, userInfo.UserName);
                    await SendMessage(roomId, "System", $"👋 {userInfo.UserName} покинул комнату");

                    if (users.Count == 0)
                    {
                        Rooms.TryRemove(roomId, out _);
                    }
                }
            }

            // Очищаем статус голосовой активности
            SpeakingUsers.TryRemove(Context.ConnectionId, out _);
        }

        // При отключении
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Находим комнату пользователя
            foreach (var room in Rooms)
            {
                if (room.Value.Contains(Context.ConnectionId))
                {
                    room.Value.Remove(Context.ConnectionId);

                    if (Users.TryGetValue(Context.ConnectionId, out var userInfo))
                    {
                        await Clients.Group(room.Key).SendAsync("UserLeft",
                            Context.ConnectionId, userInfo.UserName);
                        await SendMessage(room.Key, "System", $"👋 {userInfo.UserName} покинул комнату");

                        // Удаляем информацию о пользователе
                        Users.TryRemove(Context.ConnectionId, out _);
                    }

                    if (room.Value.Count == 0)
                    {
                        Rooms.TryRemove(room.Key, out _);
                    }

                    break;
                }
            }

            // Очищаем статус голосовой активности
            SpeakingUsers.TryRemove(Context.ConnectionId, out _);

            await base.OnDisconnectedAsync(exception);
        }
    }
}