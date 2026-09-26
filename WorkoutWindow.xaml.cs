using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Windows.Media;

namespace REPertoire
{
    public partial class WorkoutWindow : Window
    {
        private string connectionString = "Server=localhost;Database=REPertoireDB;Integrated Security=True;TrustServerCertificate=True;";
        private int currentSessionId;
        private Dictionary<int, string> sessionExercises = new Dictionary<int, string>();

        private DispatcherTimer restTimer;
        private int defaultRestSeconds = 90;
        private int currentRemainingSeconds = 90;
        private bool isTimerRunning = false;

        public WorkoutWindow()
        {
            InitializeComponent();

            StartTimeTextBox.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            EndTimeTextBox.Text = DateTime.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm");

            CreateDatabaseSession();
            LoadMasterExerciseList();

            restTimer = new DispatcherTimer();
            restTimer.Interval = TimeSpan.FromSeconds(1);
            restTimer.Tick += RestTimer_Tick;

            UpdateTimerDisplay();
        }

        private void CreateDatabaseSession()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "INSERT INTO Sessions (Name, StartTime) VALUES (@Name, @StartTime); SELECT SCOPE_IDENTITY();";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Name", WorkoutNameTextBox.Text);
                        command.Parameters.AddWithValue("@StartTime", DateTime.Parse(StartTimeTextBox.Text));
                        currentSessionId = Convert.ToInt32(command.ExecuteScalar());
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error initializing session: " + ex.Message);
            }
        }

        private void QuickAddExercise_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewExerciseName.Text)) return;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = "INSERT INTO Exercises (Name) VALUES (@Name)";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Name", NewExerciseName.Text.Trim());
                    command.ExecuteNonQuery();
                }
            }

            LoadMasterExerciseList();
            NewExerciseName.Clear();
        }

        private void LoadMasterExerciseList()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT ExerciseID, Name FROM Exercises ORDER BY Name";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                        {
                            DataTable dt = new DataTable();
                            adapter.Fill(dt);
                            ExerciseComboBox.ItemsSource = dt.DefaultView;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading exercises: " + ex.Message);
            }
        }

        private void AddExerciseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ExerciseComboBox.SelectedValue == null) return;

            int selectedId = (int)ExerciseComboBox.SelectedValue;
            DataRowView rowView = (DataRowView)ExerciseComboBox.SelectedItem;
            string selectedName = rowView["Name"].ToString();

            if (!sessionExercises.ContainsKey(selectedId))
            {
                sessionExercises.Add(selectedId, selectedName);

                ActiveExercisesList.ItemsSource = null;
                ActiveExercisesList.ItemsSource = sessionExercises;
                ActiveExercisesList.DisplayMemberPath = "Value";
                ActiveExercisesList.SelectedValuePath = "Key";

                ActiveExercisesList.SelectedValue = selectedId;
            }
        }

        private void ActiveExercisesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ActiveExercisesList.SelectedValue != null)
            {
                SetLoggingPanel.IsEnabled = true;
                var selectedKvp = (KeyValuePair<int, string>)ActiveExercisesList.SelectedItem;
                SelectedExerciseHeader.Text = selectedKvp.Value;
                RefreshSetsGrid();
            }
            else
            {
                SetLoggingPanel.IsEnabled = false;
                SelectedExerciseHeader.Text = "Select an exercise to log sets";
            }
        }

        private void LogSetBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveExercisesList.SelectedValue == null) return;

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"INSERT INTO Sets (SessionID, ExerciseID, Weight, Reps, Notes) 
                                     VALUES (@SessionID, @ExerciseID, @Weight, @Reps, @Notes)";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@SessionID", currentSessionId);
                        command.Parameters.AddWithValue("@ExerciseID", (int)ActiveExercisesList.SelectedValue);
                        command.Parameters.AddWithValue("@Weight", Convert.ToDecimal(WeightTextBox.Text));
                        command.Parameters.AddWithValue("@Reps", Convert.ToInt32(RepsTextBox.Text));
                        command.Parameters.AddWithValue("@Notes", NotesTextBox.Text ?? (object)DBNull.Value);

                        command.ExecuteNonQuery();
                    }
                }

                NotesTextBox.Clear();
                RefreshSetsGrid();
            }
            catch (FormatException)
            {
                MessageBox.Show("Please enter valid numbers for Weight and Reps.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error logging set: " + ex.Message);
            }
        }

        private void RefreshSetsGrid()
        {
            if (ActiveExercisesList.SelectedValue == null) return;

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"SELECT ROW_NUMBER() OVER (ORDER BY SetID) as SetNumber, 
                                            Weight, Reps, Notes 
                                     FROM Sets 
                                     WHERE SessionID = @SessionID AND ExerciseID = @ExerciseID
                                     ORDER BY SetID";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@SessionID", currentSessionId);
                        command.Parameters.AddWithValue("@ExerciseID", (int)ActiveExercisesList.SelectedValue);

                        using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                        {
                            DataTable dt = new DataTable();
                            adapter.Fill(dt);
                            SetsGrid.ItemsSource = dt.DefaultView;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading sets: " + ex.Message);
            }
        }

        private void SaveWorkoutBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DateTime startTime = DateTime.Parse(StartTimeTextBox.Text);
                DateTime endTime = DateTime.Parse(EndTimeTextBox.Text);
                string workoutName = WorkoutNameTextBox.Text.Trim();

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "UPDATE Sessions SET Name = @Name, StartTime = @StartTime, EndTime = @EndTime WHERE SessionID = @SessionID";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Name", workoutName);
                        command.Parameters.AddWithValue("@StartTime", startTime);
                        command.Parameters.AddWithValue("@EndTime", endTime);
                        command.Parameters.AddWithValue("@SessionID", currentSessionId);
                        command.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Workout saved successfully!");
                this.Close();
            }
            catch (FormatException)
            {
                MessageBox.Show("Please ensure times are in a valid format (e.g. yyyy-MM-dd HH:mm)");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving workout: " + ex.Message);
            }
        }

        private void ToggleTimerMenu_Click(object sender, RoutedEventArgs e)
        {
            if (TimerMenuPanel.Visibility == Visibility.Visible)
            {
                TimerMenuPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                TimerMenuPanel.Visibility = Visibility.Visible;
                MinimizedTimerBar.Visibility = Visibility.Collapsed;
            }
        }

        private void MinimizeTimer_Click(object sender, RoutedEventArgs e)
        {
            TimerMenuPanel.Visibility = Visibility.Collapsed;
            MinimizedTimerBar.Visibility = Visibility.Visible;
        }

        private void ExpandTimer_Click(object sender, MouseButtonEventArgs e)
        {
            MinimizedTimerBar.Visibility = Visibility.Collapsed;
            TimerMenuPanel.Visibility = Visibility.Visible;
        }

        private void RestTimer_Tick(object sender, EventArgs e)
        {
            if (currentRemainingSeconds > 0)
            {
                currentRemainingSeconds--;
                UpdateTimerDisplay();
            }
            else
            {
                StopAndResetTimer();
                System.Media.SystemSounds.Exclamation.Play();
            }
        }

        private void StartStopTimer_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning)
            {
                StopAndResetTimer();
            }
            else
            {
                restTimer.Start();
                isTimerRunning = true;
                StartStopTimerBtn.Content = "Stop / Reset";
                StartStopTimerBtn.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            }
        }

        private void TimerMinus_Click(object sender, RoutedEventArgs e)
        {
            AdjustTime(-15);
        }

        private void TimerPlus_Click(object sender, RoutedEventArgs e)
        {
            AdjustTime(15);
        }

        private void AdjustTime(int seconds)
        {
            currentRemainingSeconds += seconds;
            if (currentRemainingSeconds < 0) currentRemainingSeconds = 0;

            if (!isTimerRunning)
            {
                defaultRestSeconds = currentRemainingSeconds;
            }

            UpdateTimerDisplay();
        }

        private void StopAndResetTimer()
        {
            restTimer.Stop();
            isTimerRunning = false;
            currentRemainingSeconds = defaultRestSeconds;
            StartStopTimerBtn.Content = "Start";
            StartStopTimerBtn.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            UpdateTimerDisplay();
        }

        private void UpdateTimerDisplay()
        {
            string timeFormatted = TimeSpan.FromSeconds(currentRemainingSeconds).ToString(@"mm\:ss");
            TimerDisplay.Text = timeFormatted;
            MinimizedTimerDisplay.Text = timeFormatted;
        }
    }
}