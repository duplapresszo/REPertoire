using System;
using System.Windows;
using System.Windows.Controls;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;

namespace REPertoire
{
    public partial class WorkoutWindow : Window
    {
        private string connectionString = "Server=localhost;Database=REPertoireDB;Integrated Security=True;TrustServerCertificate=True;";
        private int currentSessionId;

        // Dictionary to hold exercises added to this specific session
        private Dictionary<int, string> sessionExercises = new Dictionary<int, string>();

        public WorkoutWindow()
        {
            InitializeComponent();

            // Set default times to right now when the window opens
            StartTimeTextBox.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            EndTimeTextBox.Text = DateTime.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm"); // Guesses an end time 1 hour from now

            CreateDatabaseSession();
            LoadMasterExerciseList();
        }

        private void CreateDatabaseSession()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    // We immediately insert a temporary session so we can attach sets to it
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

            // Refresh the dropdown and clear the quick-add box
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
                // Parse the user's custom times
                DateTime startTime = DateTime.Parse(StartTimeTextBox.Text);
                DateTime endTime = DateTime.Parse(EndTimeTextBox.Text);
                string workoutName = WorkoutNameTextBox.Text.Trim();

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    // Update the session with the final customized times and name
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
                this.Close(); // Closes the popup, returning to the Main Menu
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
    }
}