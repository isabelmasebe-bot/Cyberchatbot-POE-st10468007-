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

        // Quiz Variables
        private bool isInQuizMode = false;
        private int currentQuestionIndex = 0;
        private int quizScore = 0;
        private List<QuizQuestion> quizQuestions;

        // Database
        private const string ConnectionString = "Server=localhost;Database=cyberbot_db;Uid=root;Pwd=Isabel_2004*;";

        public MainWindow()
        {
            InitializeComponent();
            TestDatabaseConnection();
            InitializeResponses();
            InitializeQuiz();
            InitializeVoiceRecognition();
            ShowWelcomeMessage();
        }

        private void TestDatabaseConnection()
        {
            try
            {
                using (var conn = new MySqlConnection(ConnectionString))
                {
                    conn.Open();
                    AddBotMessage("✅ Successfully connected to MySQL Database!");
                }
            }
            catch (Exception ex)
            {
                AddBotMessage($"❌ Database Error: {ex.Message}");
            }
        }

        private void InitializeResponses()
        {
            responses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {"how are you", "I am functioning perfectly and ready to help you stay cyber safe."},
                {"purpose", "My purpose is to educate users about cybersecurity threats and protection methods."},
                {"help", "Try: add task, show tasks, start quiz, complete task, remind me, show activity log"},
                {"password", "Use strong, unique passwords with letters, numbers, and symbols."},
                {"phishing", "Never click suspicious links. Report phishing emails."}
            };
        }

        private void ProcessMessage()
        {
            string input = InputBox.Text.Trim();
            if (string.IsNullOrEmpty(input)) return;

            AddUserMessage(input);
            string lower = input.ToLower().Trim();

            // Name Handling
            if (isNameAsked || lower.StartsWith("my name is") || lower.StartsWith("i am ") || lower.StartsWith("call me "))
            {
                if (lower.StartsWith("my name is")) userName = input.Substring(11).Trim();
                else if (lower.StartsWith("i am ")) userName = input.Substring(5).Trim();
                else if (lower.StartsWith("call me ")) userName = input.Substring(8).Trim();
                else userName = input.Trim();

                isNameAsked = false;
                string greet = $"Nice to meet you, {userName}! How can I assist you today?";
                AddBotMessage(greet);
                Speak(greet);
                InputBox.Clear();
                return;
            }

            if (lower == "hi" || lower == "hello" || lower == "hey")
            {
                isNameAsked = true;
                string ask = "Hi! What's your name?";
                AddBotMessage(ask);
                Speak(ask);
                InputBox.Clear();
                return;
            }

            // Quiz
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

            // Task Commands
            if (ContainsAny(lower, new[] { "add task", "new task" }))
                HandleAddTask(input);
            else if (ContainsAny(lower, new[] { "show tasks", "view tasks", "my tasks" }))
                ShowTasks();
            else if (ContainsAny(lower, new[] { "complete", "done task", "mark done" }))
                HandleCompleteTask();
            else if (ContainsAny(lower, new[] { "delete task", "remove task" }))
                HandleDeleteTask();
            else if (ContainsAny(lower, new[] { "remind me", "set reminder" }))
                HandleReminder(input);
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
                if (input.Contains(item.Key))
                    return item.Value;
            }
            return "I'm not sure about that. Type 'help' to see available commands.";
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
                new QuizQuestion { QuestionText = "What should you do if you receive an email asking for your password?",
                    Options = new[] {"A) Reply with password", "B) Delete the email", "C) Report as phishing", "D) Ignore it"},
                    CorrectIndex = 2, Explanation = "Reporting phishing helps prevent scams." },
                new QuizQuestion { QuestionText = "True or False: Using the same password for all accounts is safe.",
                    Options = new[] {"True", "False"}, CorrectIndex = 1, Explanation = "Always use unique passwords." },
                new QuizQuestion { QuestionText = "What does 2FA stand for?",
                    Options = new[] {"A) Two Factor Authentication", "B) Two Firewalls Active"}, CorrectIndex = 0, Explanation = "Two-Factor Authentication adds extra security." },
                new QuizQuestion { QuestionText = "True or False: Public Wi-Fi is safe for banking.",
                    Options = new[] {"True", "False"}, CorrectIndex = 1, Explanation = "Use a VPN on public Wi-Fi." }
                // Add more questions here (aim for 10+)
            };
        }

        private void StartQuiz()
        {
            isInQuizMode = true;
            currentQuestionIndex = 0;
            quizScore = 0;
            AddBotMessage("🎮 **Cybersecurity Quiz Started!** Reply with A, B, C, D or True/False.");
            AskQuestion();
        }

        private void AskQuestion()
        {
            if (currentQuestionIndex >= quizQuestions.Count)
            {
                EndQuiz();
                return;
            }
            var q = quizQuestions[currentQuestionIndex];
            string msg = $"**Question {currentQuestionIndex + 1}/{quizQuestions.Count}:**\n{q.QuestionText}\n\n";
            for (int i = 0; i < q.Options.Length; i++)
                msg += q.Options[i] + "\n";
            AddBotMessage(msg);
        }

        private void HandleQuizAnswer(string answer)
        {
            var q = quizQuestions[currentQuestionIndex];
            string ans = answer.Trim().ToUpper();
            bool correct = false;

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
            double percent = (double)quizScore / quizQuestions.Count * 100;
            string result = percent >= 80 ? "Excellent! You're a cybersecurity pro!" :
                           percent >= 60 ? "Good job! Keep it up!" : "Keep practicing cybersecurity basics.";
            AddBotMessage($"🏆 Quiz Complete! Score: {quizScore}/{quizQuestions.Count} ({percent:F1}%) - {result}");
        }

        // ====================== TASK ASSISTANT (Task 1) ======================
        private void HandleAddTask(string input)
        {
            string title = input.Replace("add task", "", StringComparison.OrdinalIgnoreCase)
                               .Replace("new task", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (string.IsNullOrEmpty(title)) title = "Untitled Cybersecurity Task";

            AddTaskToDB(title, $"Cybersecurity task: {title}", null);
            AddBotMessage($"✅ Task added: **{title}**");
        }

        private void ShowTasks()
        {
            var tasks = GetActiveTasks();
            if (tasks.Count == 0)
            {
                AddBotMessage("🎉 You have no pending tasks!");
                return;
            }
            string msg = "📋 **Your Active Tasks:**\n";
            foreach (var t in tasks)
            {
                string rem = t.Reminder.HasValue ? $" (Reminder: {t.Reminder.Value:dd MMM})" : "";
                msg += $"• {t.Title}{rem}\n";
            }
            AddBotMessage(msg);
        }

        private void HandleCompleteTask()
        {
            var tasks = GetActiveTasks();
            if (tasks.Count == 0) return;
            MarkTaskAsCompleted(tasks[0].Id);
            AddBotMessage($"✅ Task '{tasks[0].Title}' marked as completed!");
        }

        private void HandleDeleteTask()
        {
            var tasks = GetActiveTasks();
            if (tasks.Count == 0) return;
            DeleteTask(tasks[0].Id);
            AddBotMessage("🗑️ Task deleted.");
        }

        private void HandleReminder(string input)
        {
            int days = 7;
            var digits = new string(input.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out int d) && d > 0) days = d;

            SetReminderOnLatestTask(DateTime.Now.AddDays(days));
            AddBotMessage($"⏰ Reminder set for {days} days.");
        }

        // DB Methods (same as before)
        private void AddTaskToDB(string title, string desc, DateTime? reminder)
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                string sql = "INSERT INTO tasks (title, description, reminder) VALUES (@title, @desc, @reminder)";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@title", title);
                    cmd.Parameters.AddWithValue("@desc", desc);
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

        // ====================== GUI HELPERS ======================
        private void AddUserMessage(string msg)
        {
            var bubble = new Border
            {
                Background = Brushes.DarkRed,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10),
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Right,
                MaxWidth = 400,
                Child = new TextBlock { Text = msg, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap }
            };
            ChatPanel.Children.Add(bubble);
            ChatScroll.ScrollToEnd();
        }

        private void AddBotMessage(string msg)
        {
            var bubble = new Border
            {
                Background = Brushes.SaddleBrown,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10),
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = 400,
                Child = new TextBlock { Text = msg, Foreground = Brushes.Black, TextWrapping = TextWrapping.Wrap }
            };
            ChatPanel.Children.Add(bubble);
            ChatScroll.ScrollToEnd();
        }

        private void Speak(string text)
        {
            speaker?.SpeakAsync(text);
        }

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
            Dispatcher.Invoke(() =>
            {
                AddUserMessage(e.Result.Text);
                // You can call ProcessMessage logic here if needed
            });
        }

        private void ShowWelcomeMessage()
        {
            string welcome = "Hello! Welcome to the Cybersecurity Awareness Bot. Type 'hi' to begin.";
            AddBotMessage(welcome);
            Speak(welcome);
        }

        // ====================== BUTTON HANDLERS (FIXED) ======================
        private void SendButton_Click(object sender, RoutedEventArgs e) => ProcessMessage();

        private void StartQuizButton_Click(object sender, RoutedEventArgs e) => StartQuiz();

        private void ViewTasksButton_Click(object sender, RoutedEventArgs e) => ShowTasks();

        private void ShowLogButton_Click(object sender, RoutedEventArgs e)
        {
            AddBotMessage("📜 Activity Log:\n(Feature coming in Task 4 - Currently under development)");
        }

        private void VoiceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddBotMessage("🎤 Listening...");
                recognizer?.RecognizeAsync(RecognizeMode.Single);
            }
            catch
            {
                AddBotMessage("Voice recognition failed.");
            }
        }

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ProcessMessage();
        }
    }

    // Task Model
    public class TaskModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime? Reminder { get; set; }
    }
}