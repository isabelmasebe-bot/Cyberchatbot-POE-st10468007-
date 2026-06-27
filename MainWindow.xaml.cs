using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Cyberchatbot_POE
{
    public partial class MainWindow : Window
    {
        private Dictionary<string, string> responses;
        private SpeechSynthesizer speaker;
        private SpeechRecognitionEngine recognizer;
        private string userName = "User";
        private bool isNameAsked = false;

        // Quiz (Task 2)
        private bool isInQuizMode = false;
        private int currentQuestionIndex = 0;
        private int quizScore = 0;
        private List<QuizQuestion> quizQuestions;

        // Activity Log (Task 4)
        private List<ActivityLogEntry> activityLog = new List<ActivityLogEntry>();

        // Database (Task 1)
        private const string ConnectionString = "Server=localhost;Database=cyberbot_db;Uid=root;Pwd=Isabel_2004*;";

        public MainWindow()
        {
            InitializeComponent();
            TestDatabaseConnection();
            InitializeResponses();
            InitializeQuiz();
            InitializeVoiceRecognition();
            ShowWelcomeMessage();
            LogActivity("Application Started");
        }

        private void LogActivity(string action)
        {
            activityLog.Add(new ActivityLogEntry { Timestamp = DateTime.Now, Action = action });
            if (activityLog.Count > 10) activityLog.RemoveAt(0);
        }

        // ====================== DATABASE METHODS (FULL) ======================
        private void TestDatabaseConnection()
        {
            try
            {
                using (var conn = new MySqlConnection(ConnectionString))
                {
                    conn.Open();
                    AddBotMessage("✅ MySQL Database Connected Successfully!");
                }
            }
            catch (Exception ex)
            {
                AddBotMessage($"❌ Database Error: {ex.Message}");
            }
        }

        private void AddTaskToDB(string title, string description, DateTime? reminder)
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                string sql = "INSERT INTO tasks (title, description, reminder) VALUES (@title, @desc, @reminder)";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@title", title);
                    cmd.Parameters.AddWithValue("@desc", description);
                    cmd.Parameters.AddWithValue("@reminder", (object)reminder ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private List<TaskModel> GetActiveTasks()
        {
            var list = new List<TaskModel>();
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                string sql = "SELECT * FROM tasks WHERE is_completed = 0 ORDER BY created_at DESC";
                using (var cmd = new MySqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new TaskModel
                        {
                            Id = reader.GetInt32("id"),
                            Title = reader.GetString("title"),
                            Reminder = reader.IsDBNull("reminder") ? null : reader.GetDateTime("reminder")
                        });
                    }
                }
            }
            return list;
        }

        private void MarkTaskAsCompleted(int id)
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                string sql = "UPDATE tasks SET is_completed = 1 WHERE id = @id";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void DeleteTask(int id)
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                string sql = "DELETE FROM tasks WHERE id = @id";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void SetReminderOnLatestTask(DateTime reminderDate)
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                string sql = "UPDATE tasks SET reminder = @rem WHERE id = (SELECT id FROM tasks ORDER BY created_at DESC LIMIT 1)";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@rem", reminderDate);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ====================== RESPONSES & PROCESSING ======================
        private void InitializeResponses()
        {
            responses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {"how are you", "I am functioning perfectly and ready to help you stay cyber safe."},
                {"purpose", "My purpose is to educate users about cybersecurity threats and protection methods."},
                {"help", "Try: add task, show tasks, start quiz, show activity log, remind me"},
                {"password", "Use strong unique passwords with mixed characters."},
                {"phishing", "Never click suspicious links. Report phishing emails."}
            };
        }

        private void ProcessMessage()
        {
            string input = InputBox.Text.Trim();
            if (string.IsNullOrEmpty(input)) return;

            AddUserMessage(input);
            string lower = input.ToLower().Trim();

            if (isNameAsked || lower.StartsWith("my name is") || lower.StartsWith("i am ") || lower.StartsWith("call me "))
            {
                if (lower.StartsWith("my name is")) userName = input.Substring(11).Trim();
                else if (lower.StartsWith("i am ")) userName = input.Substring(5).Trim();
                else if (lower.StartsWith("call me ")) userName = input.Substring(8).Trim();
                else userName = input.Trim();

                isNameAsked = false;
                string greet = $"Nice to meet you, {userName}!";
                AddBotMessage(greet);
                Speak(greet);
                InputBox.Clear();
                return;
            }

            if (lower == "hi" || lower == "hello" || lower == "hey")
            {
                isNameAsked = true;
                AddBotMessage("Hi! What's your name?");
                Speak("Hi! What's your name?");
                InputBox.Clear();
                return;
            }

            if (lower.Contains("start quiz") || lower.Contains("play quiz"))
            {
                StartQuiz();
                InputBox.Clear();
                return;
            }
            if (isInQuizMode)
            {
                HandleQuizAnswer(input);
                InputBox.Clear();
                return;
            }

            if (ContainsAny(lower, new[] { "add task", "new task" })) HandleAddTask(input);
            else if (ContainsAny(lower, new[] { "show tasks", "view tasks", "my tasks" })) ShowTasks();
            else if (ContainsAny(lower, new[] { "complete", "done task", "mark done" })) HandleCompleteTask();
            else if (ContainsAny(lower, new[] { "delete task", "remove task" })) HandleDeleteTask();
            else if (ContainsAny(lower, new[] { "remind me", "set reminder" })) HandleReminder(input);
            else if (ContainsAny(lower, new[] { "activity log", "show log", "what have you done" })) ShowActivityLog();
            else
            {
                string response = GenerateResponse(lower);
                AddBotMessage(response);
                Speak(response);
            }

            InputBox.Clear();
        }

        private string GenerateResponse(string input)
        {
            foreach (var item in responses.OrderByDescending(x => x.Key.Length))
            {
                if (input.Contains(item.Key)) return item.Value;
            }
            return "I don't understand. Type 'help' for commands.";
        }

        private bool ContainsAny(string input, string[] keywords)
        {
            return keywords.Any(k => input.Contains(k));
        }

        // ====================== QUIZ (Task 2) ======================
        private class QuizQuestion
        {
            public string QuestionText { get; set; }
            public string[] Options { get; set; }
            public int CorrectIndex { get; set; }
            public string Explanation { get; set; }
        }

        private void InitializeQuiz()
        {
            quizQuestions = new List<QuizQuestion>
            {
                new QuizQuestion { QuestionText = "What should you do if you receive an email asking for your password?", Options = new[] {"A) Reply", "B) Delete", "C) Report as phishing", "D) Ignore"}, CorrectIndex = 2, Explanation = "Reporting helps prevent scams." },
                new QuizQuestion { QuestionText = "True or False: Using the same password everywhere is safe.", Options = new[] {"True", "False"}, CorrectIndex = 1, Explanation = "Always use unique passwords." },
                new QuizQuestion { QuestionText = "What does 2FA stand for?", Options = new[] {"A) Two Factor Authentication", "B) Two Firewalls Active"}, CorrectIndex = 0, Explanation = "Two-Factor Authentication adds extra security." }
            };
        }

        private void StartQuiz()
        {
            isInQuizMode = true;
            currentQuestionIndex = 0;
            quizScore = 0;
            LogActivity("Quiz Started");
            AddBotMessage("🎮 Quiz Started! Answer with A/B/C/D or True/False.");
            AskQuestion();
        }

        private void AskQuestion()
        {
            if (currentQuestionIndex >= quizQuestions.Count) { EndQuiz(); return; }
            var q = quizQuestions[currentQuestionIndex];
            string msg = $"**Question {currentQuestionIndex + 1}:** {q.QuestionText}\n\n";
            foreach (var opt in q.Options) msg += opt + "\n";
            AddBotMessage(msg);
        }

        private void HandleQuizAnswer(string answer)
        {
            var q = quizQuestions[currentQuestionIndex];
            bool correct = false;
            string ans = answer.Trim().ToUpper();
            if (ans == "A" || ans == "TRUE") correct = q.CorrectIndex == 0;
            else if (ans == "B" || ans == "FALSE") correct = q.CorrectIndex == 1;
            else if (ans == "C") correct = q.CorrectIndex == 2;
            else if (ans == "D") correct = q.CorrectIndex == 3;

            if (correct) quizScore++;
            AddBotMessage(correct ? "✅ Correct!" : $"❌ Wrong! Correct: {q.Options[q.CorrectIndex]}");
            AddBotMessage("💡 " + q.Explanation);
            currentQuestionIndex++;
            AskQuestion();
        }

        private void EndQuiz()
        {
            isInQuizMode = false;
            LogActivity($"Quiz Completed - Score: {quizScore}/{quizQuestions.Count}");
            double percent = (double)quizScore / quizQuestions.Count * 100;
            AddBotMessage($"🏆 Quiz Finished! Score: {quizScore}/{quizQuestions.Count} ({percent:F1}%)");
        }

        // ====================== TASK ASSISTANT (Task 1) ======================
        private void HandleAddTask(string input)
        {
            string title = input.Replace("add task", "").Replace("new task", "").Trim();
            if (string.IsNullOrEmpty(title)) title = "Untitled Task";
            AddTaskToDB(title, $"Cybersecurity task: {title}", null);
            LogActivity($"Task Added: {title}");
            AddBotMessage($"✅ Task added: {title}");
        }

        private void ShowTasks()
        {
            var tasks = GetActiveTasks();
            if (tasks.Count == 0)
            {
                AddBotMessage("You have no pending tasks.");
                return;
            }
            string msg = "📋 Your Active Tasks:\n";
            foreach (var t in tasks)
            {
                string rem = t.Reminder.HasValue ? $" (Reminder: {t.Reminder.Value:dd MMM})" : "";
                msg += $"- {t.Title}{rem}\n";
            }
            AddBotMessage(msg);
        }

        private void HandleCompleteTask()
        {
            var tasks = GetActiveTasks();
            if (tasks.Count == 0) return;
            MarkTaskAsCompleted(tasks[0].Id);
            LogActivity($"Task Completed: {tasks[0].Title}");
            AddBotMessage($"✅ Task '{tasks[0].Title}' marked as completed!");
        }

        private void HandleDeleteTask()
        {
            var tasks = GetActiveTasks();
            if (tasks.Count == 0) return;
            DeleteTask(tasks[0].Id);
            LogActivity($"Task Deleted: {tasks[0].Title}");
            AddBotMessage("🗑️ Task deleted.");
        }

        private void HandleReminder(string input)
        {
            int days = 7;
            var digits = new string(input.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out int d) && d > 0) days = d;
            SetReminderOnLatestTask(DateTime.Now.AddDays(days));
            LogActivity($"Reminder Set for {days} days");
            AddBotMessage($"⏰ Reminder set for {days} days.");
        }

        // ====================== ACTIVITY LOG (Task 4) ======================
        private void ShowActivityLog()
        {
            LogActivity("Activity Log Viewed");
            if (activityLog.Count == 0)
            {
                AddBotMessage("No activities yet.");
                return;
            }
            string log = "📜 **Recent Activity Log**\n\n";
            foreach (var entry in activityLog.OrderByDescending(a => a.Timestamp))
            {
                log += $"[{entry.Timestamp:HH:mm}] {entry.Action}\n";
            }
            AddBotMessage(log);
        }

        private class ActivityLogEntry
        {
            public DateTime Timestamp { get; set; }
            public string Action { get; set; }
        }

        // ====================== GUI HELPERS ======================
        private void AddUserMessage(string msg)
        {
            var bubble = new Border
            {
                Background = Brushes.Blue,           // User Color: Blue
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12),
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Right,
                MaxWidth = 400
            };
            bubble.Child = new TextBlock { Text = msg, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap };
            ChatPanel.Children.Add(bubble);
            ChatScroll.ScrollToEnd();
        }

        private void AddBotMessage(string msg)
        {
            var bubble = new Border
            {
                Background = Brushes.Green,          // Bot Color: Green
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12),
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = 400
            };
            bubble.Child = new TextBlock { Text = msg, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap };
            ChatPanel.Children.Add(bubble);
            ChatScroll.ScrollToEnd();
        }

        private void Speak(string text) => speaker?.SpeakAsync(text);

        private void InitializeVoiceRecognition()
        {
            speaker = new SpeechSynthesizer();
            try
            {
                recognizer = new SpeechRecognitionEngine();
                recognizer.SetInputToDefaultAudioDevice();
                recognizer.LoadGrammar(new DictationGrammar());
                recognizer.SpeechRecognized += Recognizer_SpeechRecognized;
            }
            catch { AddBotMessage("Voice recognition unavailable."); }
        }

        private void Recognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            Dispatcher.Invoke(() => AddUserMessage(e.Result.Text));
        }

        private void ShowWelcomeMessage()
        {
            string welcome = "Hello! Welcome to the Cybersecurity Awareness Bot. Type 'hi' to begin.";
            AddBotMessage(welcome);
            Speak(welcome);
        }

        private void SendButton_Click(object sender, RoutedEventArgs e) => ProcessMessage();
        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ProcessMessage();
        }

        private void StartQuizButton_Click(object sender, RoutedEventArgs e) => StartQuiz();
        private void ViewTasksButton_Click(object sender, RoutedEventArgs e) => ShowTasks();
        private void ShowLogButton_Click(object sender, RoutedEventArgs e) => ShowActivityLog();
        private void ClearChatButton_Click(object sender, RoutedEventArgs e)
        {
            ChatPanel.Children.Clear();
            LogActivity("Chat Cleared");
            ShowWelcomeMessage();
        }
        private void VoiceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddBotMessage("🎤 Listening...");
                recognizer?.RecognizeAsync(RecognizeMode.Single);
            }
            catch { AddBotMessage("Voice failed."); }
        }
    }

    // Models
    public class TaskModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public DateTime? Reminder { get; set; }
    }

    public class QuizQuestion
    {
        public string QuestionText { get; set; }
        public string[] Options { get; set; }
        public int CorrectIndex { get; set; }
        public string Explanation { get; set; }
    }

    public class ActivityLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Action { get; set; }
    }
}