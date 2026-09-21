CREATE TABLE Exercises (
    ExerciseID INT IDENTITY(1,1) PRIMARY KEY,
    Name VARCHAR(100) UNIQUE NOT NULL
);

CREATE TABLE Sessions (
    SessionID INT IDENTITY(1,1) PRIMARY KEY,
    StartTime DATETIME DEFAULT CURRENT_TIMESTAMP NOT NULL,
    EndTime DATETIME
);

CREATE TABLE Sets (
    SetID INT IDENTITY(1,1) PRIMARY KEY,
    SessionID INT NOT NULL,
    ExerciseID INT NOT NULL,
    Weight DECIMAL(5,2) NOT NULL,
    Reps INT NOT NULL,
    Notes VARCHAR(1000),
    
    CONSTRAINT fk_session FOREIGN KEY (SessionID) REFERENCES Sessions(SessionID),
    CONSTRAINT fk_exercise FOREIGN KEY (ExerciseID) REFERENCES Exercises(ExerciseID)
);

INSERT INTO Exercises (Name) VALUES ('Barbell Back Squat');
INSERT INTO Exercises (Name) VALUES ('Bench Press');
INSERT INTO Exercises (Name) VALUES ('Deadlift');
INSERT INTO Exercises (Name) VALUES ('Overhead Press');
INSERT INTO Exercises (Name) VALUES ('Pull-up');