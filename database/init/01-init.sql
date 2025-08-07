-- CEPEM Healthcare Database Initialization
CREATE DATABASE IF NOT EXISTS cepem_healthcare;
USE cepem_healthcare;

-- Grant privileges to user
GRANT ALL PRIVILEGES ON cepem_healthcare.* TO 'cepem_user'@'%';
FLUSH PRIVILEGES;

-- Create Doctors table
CREATE TABLE IF NOT EXISTS Doctors (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    FirstName VARCHAR(100) NOT NULL,
    LastName VARCHAR(100) NOT NULL,
    Email VARCHAR(255) NOT NULL UNIQUE,
    PasswordHash TEXT NOT NULL,
    Salt VARCHAR(255) NOT NULL,
    Specialization VARCHAR(150),
    LicenseNumber VARCHAR(50) UNIQUE,
    IsActive BOOLEAN DEFAULT TRUE,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    LastLoginAt DATETIME NULL
);

-- Insert default data will be handled by Entity Framework seeding
