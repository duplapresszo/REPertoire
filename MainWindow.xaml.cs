using System;
using System.Windows;
using System.Windows.Controls;
using System.Data;
using Microsoft.Data.SqlClient;

namespace REPertoire
{
    public partial class MainWindow : Window
    {
        private string connectionString = "Server=localhost;Database=REPertoireDB;Integrated Security=True;TrustServerCertificate=True;";

        public MainWindow()
        {
            InitializeComponent();
            LoadHistory();
            LoadExercises();
        }

        // Helper class to bind data to the History ListBox
        public class HistoryItem
        {
            public string Title { get; set; }
            public string Summary { get; set; }
        }

        private void LoadHistory()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    // Using our new aggregated query
                    // Update your query string inside the LoadHistory() method to this:
                    string query = @"
                        SELECT 
                            s.SessionID,
                            s.Name,
                            s.StartTime,
                            s.EndTime,
                            DATEDIFF(MINUTE, s.StartTime, ISNULL(s.EndTime, s.StartTime)) AS DurationMinutes,
                            STRING_AGG(CAST(set_counts.SetCount AS VARCHAR) + ' x ' + set_counts.ExerciseName, CHAR(13) + CHAR(10)) AS ExerciseSummary
                        FROM Sessions s
                        LEFT JOIN (
                            SELECT st.SessionID, st.ExerciseID, MAX(e.Name) AS ExerciseName, COUNT(*) AS SetCount
                            FROM Sets st
                            JOIN Exercises e ON st.ExerciseID = e.ExerciseID
                            GROUP BY st.SessionID, st.ExerciseID
                        ) set_counts ON s.SessionID = set_counts.SessionID
                        GROUP BY s.SessionID, s.Name, s.StartTime, s.EndTime
                           ORDER BY s.StartTime DESC";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            var historyItems = new System.Collections.Generic.List<HistoryItem>();
                            while (reader.Read())
                            {
                                string name = reader["Name"] != DBNull.Value ? reader["Name"].ToString() : "Untitled Workout";
                                DateTime date = Convert.ToDateTime(reader["StartTime"]);
                                int duration = Convert.ToInt32(reader["DurationMinutes"]);
                                string summary = reader["ExerciseSummary"] != DBNull.Value ? reader["ExerciseSummary"].ToString() : "No exercises logged.";

                                historyItems.Add(new HistoryItem
                                {
                                    Title = $"{name}  —  {date:dd MMM yyyy} ({duration} min)",
                                    Summary = summary
                                });
                            }
                            // Bind the list to the ListBox
                            HistoryListBox.ItemsSource = historyItems;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading history: " + ex.Message);
            }
        }

        private void LoadExercises()
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
                            ExercisesListBox.ItemsSource = dt.DefaultView;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading exercises: " + ex.Message);
            }
        }

        private void ExercisesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ExercisesListBox.SelectedItem != null)
            {
                DataRowView row = (DataRowView)ExercisesListBox.SelectedItem;
                EditExerciseNameTextBox.Text = row["Name"].ToString();
            }
        }

        private void AddExerciseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EditExerciseNameTextBox.Text)) return;
            ExecuteExerciseQuery("INSERT INTO Exercises (Name) VALUES (@Name)");
        }

        private void RenameExerciseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ExercisesListBox.SelectedValue == null || string.IsNullOrWhiteSpace(EditExerciseNameTextBox.Text)) return;
            ExecuteExerciseQuery("UPDATE Exercises SET Name = @Name WHERE ExerciseID = @ID", (int)ExercisesListBox.SelectedValue);
        }

        private void DeleteExerciseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ExercisesListBox.SelectedValue == null) return;

            var result = MessageBox.Show("Are you sure? This will fail if this exercise is linked to past workouts.", "Confirm Delete", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                ExecuteExerciseQuery("DELETE FROM Exercises WHERE ExerciseID = @ID", (int)ExercisesListBox.SelectedValue);
            }
        }

        private void ExecuteExerciseQuery(string query, int? id = null)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Name", EditExerciseNameTextBox.Text.Trim());
                        if (id.HasValue) command.Parameters.AddWithValue("@ID", id.Value);
                        command.ExecuteNonQuery();
                    }
                }
                LoadExercises();
                EditExerciseNameTextBox.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error modifying exercise: " + ex.Message);
            }
        }

        private void StartWorkoutBtn_Click(object sender, RoutedEventArgs e)
        {
            // Open the new window as a popup dialog
            WorkoutWindow workoutWindow = new WorkoutWindow();
            workoutWindow.ShowDialog();

            // When the workout window is closed, refresh the history list
            LoadHistory();
        }
    }
}