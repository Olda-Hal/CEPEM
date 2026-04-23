CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;

ALTER DATABASE CHARACTER SET utf8mb4;

CREATE TABLE `ActivityLogs` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Timestamp` datetime(6) NOT NULL,
    `Service` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Action` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Details` longtext CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_ActivityLogs` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Comments` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Text` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Comments` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `DrugCategories` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` longtext CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_DrugCategories` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Drugs` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Drugs` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Equipment` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Equipment` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `EventTypes` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` int NULL,
    CONSTRAINT `PK_EventTypes` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `ExaminationTypes` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_ExaminationTypes` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Hospitals` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Address` longtext CHARACTER SET utf8mb4 NULL,
    `Active` tinyint(1) NULL,
    CONSTRAINT `PK_Hospitals` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `InjuryTypes` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_InjuryTypes` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Roles` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Roles` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Symptoms` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Symptoms` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `VaccineTypes` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_VaccineTypes` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Persons` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `FirstName` longtext CHARACTER SET utf8mb4 NOT NULL,
    `LastName` longtext CHARACTER SET utf8mb4 NOT NULL,
    `TitleBefore` longtext CHARACTER SET utf8mb4 NULL,
    `TitleAfter` longtext CHARACTER SET utf8mb4 NULL,
    `UID` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Active` tinyint(1) NOT NULL,
    `Email` longtext CHARACTER SET utf8mb4 NOT NULL,
    `PhoneNumber` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `Gender` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CommentId` int NULL,
    CONSTRAINT `PK_Persons` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Persons_Comments_CommentId` FOREIGN KEY (`CommentId`) REFERENCES `Comments` (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `DrugToDrugCategories` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `CategoryId` int NULL,
    `DrugId` int NOT NULL,
    CONSTRAINT `PK_DrugToDrugCategories` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_DrugToDrugCategories_DrugCategories_CategoryId` FOREIGN KEY (`CategoryId`) REFERENCES `DrugCategories` (`Id`),
    CONSTRAINT `FK_DrugToDrugCategories_Drugs_DrugId` FOREIGN KEY (`DrugId`) REFERENCES `Drugs` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `HospitalEquipment` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `HospitalId` int NOT NULL,
    `EquipmentId` int NOT NULL,
    CONSTRAINT `PK_HospitalEquipment` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_HospitalEquipment_Equipment_EquipmentId` FOREIGN KEY (`EquipmentId`) REFERENCES `Equipment` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_HospitalEquipment_Hospitals_HospitalId` FOREIGN KEY (`HospitalId`) REFERENCES `Hospitals` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `EmailHistories` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Email` longtext CHARACTER SET utf8mb4 NOT NULL,
    `PersonId` int NOT NULL,
    `UsedFrom` datetime(6) NOT NULL,
    `UsedTo` datetime(6) NOT NULL,
    CONSTRAINT `PK_EmailHistories` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EmailHistories_Persons_PersonId` FOREIGN KEY (`PersonId`) REFERENCES `Persons` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `Employees` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PersonId` int NOT NULL,
    `PasswordHash` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Salt` longtext CHARACTER SET utf8mb4 NOT NULL,
    `LastLoginAt` datetime(6) NULL,
    CONSTRAINT `PK_Employees` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Employees_Persons_PersonId` FOREIGN KEY (`PersonId`) REFERENCES `Persons` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `FirstNameHistories` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `FirstName` longtext CHARACTER SET utf8mb4 NOT NULL,
    `PersonId` int NOT NULL,
    `UsedFrom` datetime(6) NOT NULL,
    `UsedTo` datetime(6) NOT NULL,
    CONSTRAINT `PK_FirstNameHistories` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_FirstNameHistories_Persons_PersonId` FOREIGN KEY (`PersonId`) REFERENCES `Persons` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `LastNameHistories` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `LastName` longtext CHARACTER SET utf8mb4 NOT NULL,
    `PersonId` int NOT NULL,
    `UsedFrom` datetime(6) NOT NULL,
    `UsedTo` datetime(6) NOT NULL,
    CONSTRAINT `PK_LastNameHistories` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_LastNameHistories_Persons_PersonId` FOREIGN KEY (`PersonId`) REFERENCES `Persons` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `Patients` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PersonId` int NOT NULL,
    `BirthDate` datetime(6) NOT NULL,
    `InsuranceNumber` int NOT NULL,
    `CommentId` int NULL,
    `Alive` tinyint(1) NOT NULL,
    CONSTRAINT `PK_Patients` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Patients_Comments_CommentId` FOREIGN KEY (`CommentId`) REFERENCES `Comments` (`Id`),
    CONSTRAINT `FK_Patients_Persons_PersonId` FOREIGN KEY (`PersonId`) REFERENCES `Persons` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `PhoneNumberHistories` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PhoneNumber` longtext CHARACTER SET utf8mb4 NOT NULL,
    `PersonId` int NOT NULL,
    `UsedFrom` datetime(6) NOT NULL,
    `UsedTo` datetime(6) NOT NULL,
    CONSTRAINT `PK_PhoneNumberHistories` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_PhoneNumberHistories_Persons_PersonId` FOREIGN KEY (`PersonId`) REFERENCES `Persons` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `UserRoles` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `UserId` int NOT NULL,
    `RoleId` int NOT NULL,
    CONSTRAINT `PK_UserRoles` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_UserRoles_Persons_UserId` FOREIGN KEY (`UserId`) REFERENCES `Persons` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_UserRoles_Roles_RoleId` FOREIGN KEY (`RoleId`) REFERENCES `Roles` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `HospitalEmployees` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `HospitalId` int NOT NULL,
    `EmployeeId` int NOT NULL,
    CONSTRAINT `PK_HospitalEmployees` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_HospitalEmployees_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_HospitalEmployees_Hospitals_HospitalId` FOREIGN KEY (`HospitalId`) REFERENCES `Hospitals` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `Events` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PatientId` int NOT NULL,
    `EventTypeId` int NOT NULL,
    `HappenedAt` datetime(6) NOT NULL,
    `HappenedTo` datetime(6) NULL,
    `CommentId` int NULL,
    CONSTRAINT `PK_Events` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Events_Comments_CommentId` FOREIGN KEY (`CommentId`) REFERENCES `Comments` (`Id`),
    CONSTRAINT `FK_Events_EventTypes_EventTypeId` FOREIGN KEY (`EventTypeId`) REFERENCES `EventTypes` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_Events_Patients_PatientId` FOREIGN KEY (`PatientId`) REFERENCES `Patients` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `Appointments` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PersonId` int NOT NULL,
    `EmployeeId` int NOT NULL,
    `EquipmentId` int NULL,
    `StartTime` datetime(6) NOT NULL,
    `EndTime` datetime(6) NOT NULL,
    `HospitalId` int NOT NULL,
    CONSTRAINT `PK_Appointments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Appointments_Equipment_EquipmentId` FOREIGN KEY (`EquipmentId`) REFERENCES `Equipment` (`Id`),
    CONSTRAINT `FK_Appointments_HospitalEmployees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `HospitalEmployees` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_Appointments_Hospitals_HospitalId` FOREIGN KEY (`HospitalId`) REFERENCES `Hospitals` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_Appointments_Persons_PersonId` FOREIGN KEY (`PersonId`) REFERENCES `Persons` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `DrugUses` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `DrugId` int NOT NULL,
    `EventId` int NOT NULL,
    CONSTRAINT `PK_DrugUses` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_DrugUses_Drugs_DrugId` FOREIGN KEY (`DrugId`) REFERENCES `Drugs` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_DrugUses_Events_EventId` FOREIGN KEY (`EventId`) REFERENCES `Events` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `Examinations` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ExaminationTypeId` int NOT NULL,
    `EventId` int NOT NULL,
    CONSTRAINT `PK_Examinations` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Examinations_Events_EventId` FOREIGN KEY (`EventId`) REFERENCES `Events` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_Examinations_ExaminationTypes_ExaminationTypeId` FOREIGN KEY (`ExaminationTypeId`) REFERENCES `ExaminationTypes` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `Injuries` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `EventId` int NOT NULL,
    `InjuryTypeId` int NOT NULL,
    CONSTRAINT `PK_Injuries` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Injuries_Events_EventId` FOREIGN KEY (`EventId`) REFERENCES `Events` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_Injuries_InjuryTypes_InjuryTypeId` FOREIGN KEY (`InjuryTypeId`) REFERENCES `InjuryTypes` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `PatientSymptoms` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `SymptomId` int NOT NULL,
    `EventId` int NOT NULL,
    CONSTRAINT `PK_PatientSymptoms` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_PatientSymptoms_Events_EventId` FOREIGN KEY (`EventId`) REFERENCES `Events` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_PatientSymptoms_Symptoms_SymptomId` FOREIGN KEY (`SymptomId`) REFERENCES `Symptoms` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `Pregnancies` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `EventId` int NOT NULL,
    `Result` tinyint(1) NOT NULL,
    CONSTRAINT `PK_Pregnancies` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Pregnancies_Events_EventId` FOREIGN KEY (`EventId`) REFERENCES `Events` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `Vaccines` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `EventId` int NOT NULL,
    `VaccineTypeId` int NOT NULL,
    CONSTRAINT `PK_Vaccines` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Vaccines_Events_EventId` FOREIGN KEY (`EventId`) REFERENCES `Events` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_Vaccines_VaccineTypes_VaccineTypeId` FOREIGN KEY (`VaccineTypeId`) REFERENCES `VaccineTypes` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_Appointments_EmployeeId` ON `Appointments` (`EmployeeId`);

CREATE INDEX `IX_Appointments_EquipmentId` ON `Appointments` (`EquipmentId`);

CREATE INDEX `IX_Appointments_HospitalId` ON `Appointments` (`HospitalId`);

CREATE INDEX `IX_Appointments_PersonId` ON `Appointments` (`PersonId`);

CREATE INDEX `IX_DrugToDrugCategories_CategoryId` ON `DrugToDrugCategories` (`CategoryId`);

CREATE INDEX `IX_DrugToDrugCategories_DrugId` ON `DrugToDrugCategories` (`DrugId`);

CREATE INDEX `IX_DrugUses_DrugId` ON `DrugUses` (`DrugId`);

CREATE INDEX `IX_DrugUses_EventId` ON `DrugUses` (`EventId`);

CREATE INDEX `IX_EmailHistories_PersonId` ON `EmailHistories` (`PersonId`);

CREATE UNIQUE INDEX `IX_Employees_PersonId` ON `Employees` (`PersonId`);

CREATE INDEX `IX_Events_CommentId` ON `Events` (`CommentId`);

CREATE INDEX `IX_Events_EventTypeId` ON `Events` (`EventTypeId`);

CREATE INDEX `IX_Events_PatientId` ON `Events` (`PatientId`);

CREATE INDEX `IX_Examinations_EventId` ON `Examinations` (`EventId`);

CREATE INDEX `IX_Examinations_ExaminationTypeId` ON `Examinations` (`ExaminationTypeId`);

CREATE INDEX `IX_FirstNameHistories_PersonId` ON `FirstNameHistories` (`PersonId`);

CREATE INDEX `IX_HospitalEmployees_EmployeeId` ON `HospitalEmployees` (`EmployeeId`);

CREATE INDEX `IX_HospitalEmployees_HospitalId` ON `HospitalEmployees` (`HospitalId`);

CREATE INDEX `IX_HospitalEquipment_EquipmentId` ON `HospitalEquipment` (`EquipmentId`);

CREATE INDEX `IX_HospitalEquipment_HospitalId` ON `HospitalEquipment` (`HospitalId`);

CREATE INDEX `IX_Injuries_EventId` ON `Injuries` (`EventId`);

CREATE INDEX `IX_Injuries_InjuryTypeId` ON `Injuries` (`InjuryTypeId`);

CREATE INDEX `IX_LastNameHistories_PersonId` ON `LastNameHistories` (`PersonId`);

CREATE INDEX `IX_Patients_CommentId` ON `Patients` (`CommentId`);

CREATE UNIQUE INDEX `IX_Patients_PersonId` ON `Patients` (`PersonId`);

CREATE INDEX `IX_PatientSymptoms_EventId` ON `PatientSymptoms` (`EventId`);

CREATE INDEX `IX_PatientSymptoms_SymptomId` ON `PatientSymptoms` (`SymptomId`);

CREATE INDEX `IX_Persons_CommentId` ON `Persons` (`CommentId`);

CREATE INDEX `IX_PhoneNumberHistories_PersonId` ON `PhoneNumberHistories` (`PersonId`);

CREATE INDEX `IX_Pregnancies_EventId` ON `Pregnancies` (`EventId`);

CREATE INDEX `IX_UserRoles_RoleId` ON `UserRoles` (`RoleId`);

CREATE INDEX `IX_UserRoles_UserId` ON `UserRoles` (`UserId`);

CREATE INDEX `IX_Vaccines_EventId` ON `Vaccines` (`EventId`);

CREATE INDEX `IX_Vaccines_VaccineTypeId` ON `Vaccines` (`VaccineTypeId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20250816205344_InitialDatabaseStructure', '8.0.0');

COMMIT;

START TRANSACTION;

ALTER TABLE `Employees` ADD `PasswordExpiration` datetime(6) NOT NULL DEFAULT '0001-01-01 00:00:00';

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20250817162528_PasswordExpiration', '8.0.0');

COMMIT;

START TRANSACTION;

UPDATE `EventTypes` SET `Name` = ''
WHERE `Name` IS NULL;
SELECT ROW_COUNT();


ALTER TABLE `EventTypes` MODIFY COLUMN `Name` longtext CHARACTER SET utf8mb4 NOT NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20250929093916_UpdateEventTypeNameToString', '8.0.0');

COMMIT;

START TRANSACTION;

ALTER TABLE `Events` ADD `EventGroupId` char(36) COLLATE ascii_general_ci NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20251010141501_AddEventGroupId', '8.0.0');

COMMIT;

START TRANSACTION;

CREATE TABLE `DrugTranslations` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `DrugId` int NOT NULL,
    `Language` varchar(10) CHARACTER SET utf8mb4 NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_DrugTranslations` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_DrugTranslations_Drugs_DrugId` FOREIGN KEY (`DrugId`) REFERENCES `Drugs` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `EventTypeTranslations` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `EventTypeId` int NOT NULL,
    `Language` varchar(10) CHARACTER SET utf8mb4 NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_EventTypeTranslations` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EventTypeTranslations_EventTypes_EventTypeId` FOREIGN KEY (`EventTypeId`) REFERENCES `EventTypes` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `ExaminationTypeTranslations` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ExaminationTypeId` int NOT NULL,
    `Language` varchar(10) CHARACTER SET utf8mb4 NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_ExaminationTypeTranslations` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ExaminationTypeTranslations_ExaminationTypes_ExaminationType~` FOREIGN KEY (`ExaminationTypeId`) REFERENCES `ExaminationTypes` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE UNIQUE INDEX `IX_DrugTranslations_DrugId_Language` ON `DrugTranslations` (`DrugId`, `Language`);

CREATE UNIQUE INDEX `IX_EventTypeTranslations_EventTypeId_Language` ON `EventTypeTranslations` (`EventTypeId`, `Language`);

CREATE UNIQUE INDEX `IX_ExaminationTypeTranslations_ExaminationTypeId_Language` ON `ExaminationTypeTranslations` (`ExaminationTypeId`, `Language`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20251014123646_AddTranslationTables', '8.0.0');

COMMIT;

START TRANSACTION;

ALTER TABLE `Patients` ADD `PhotoPath` longtext CHARACTER SET utf8mb4 NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20251103091558_AddPatientPhotoPath', '8.0.0');

COMMIT;

START TRANSACTION;

CREATE TABLE `PatientDocuments` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PatientId` int NOT NULL,
    `FileName` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `OriginalFileName` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `UploadedAt` datetime(6) NOT NULL,
    `FileSize` bigint NOT NULL,
    `EncryptedPath` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_PatientDocuments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_PatientDocuments_Patients_PatientId` FOREIGN KEY (`PatientId`) REFERENCES `Patients` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_PatientDocuments_PatientId` ON `PatientDocuments` (`PatientId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20251104133551_AddPatientDocuments', '8.0.0');

COMMIT;

START TRANSACTION;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20251104141345_FixPatientDocumentRelationship', '8.0.0');

COMMIT;

START TRANSACTION;

ALTER TABLE `PatientDocuments` ADD `IsDeleted` tinyint(1) NOT NULL DEFAULT FALSE;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20251104152549_AddIsDeletedToPatientDocument', '8.0.0');

COMMIT;

START TRANSACTION;

CREATE TABLE `ExaminationRooms` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Description` varchar(500) CHARACTER SET utf8mb4 NULL,
    `HospitalId` int NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_ExaminationRooms` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ExaminationRooms_Hospitals_HospitalId` FOREIGN KEY (`HospitalId`) REFERENCES `Hospitals` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `DoctorExaminationRooms` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `DoctorId` int NOT NULL,
    `ExaminationRoomId` int NOT NULL,
    `AssignedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_DoctorExaminationRooms` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_DoctorExaminationRooms_Employees_DoctorId` FOREIGN KEY (`DoctorId`) REFERENCES `Employees` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_DoctorExaminationRooms_ExaminationRooms_ExaminationRoomId` FOREIGN KEY (`ExaminationRoomId`) REFERENCES `ExaminationRooms` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `Reservations` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ExaminationRoomId` int NOT NULL,
    `DoctorId` int NOT NULL,
    `PersonId` int NOT NULL,
    `ExaminationTypeId` int NOT NULL,
    `StartDateTime` datetime(6) NOT NULL,
    `EndDateTime` datetime(6) NOT NULL,
    `Notes` varchar(1000) CHARACTER SET utf8mb4 NULL,
    `Status` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_Reservations` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Reservations_Employees_DoctorId` FOREIGN KEY (`DoctorId`) REFERENCES `Employees` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_Reservations_ExaminationRooms_ExaminationRoomId` FOREIGN KEY (`ExaminationRoomId`) REFERENCES `ExaminationRooms` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_Reservations_ExaminationTypes_ExaminationTypeId` FOREIGN KEY (`ExaminationTypeId`) REFERENCES `ExaminationTypes` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_Reservations_Persons_PersonId` FOREIGN KEY (`PersonId`) REFERENCES `Persons` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `IntakeFormLinks` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `TokenHash` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `PersonId` int NOT NULL,
    `ReservationId` int NULL,
    `ExpiresAtUtc` datetime(6) NOT NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    `UsedAtUtc` datetime(6) NULL,
    `RevokedAtUtc` datetime(6) NULL,
    CONSTRAINT `PK_IntakeFormLinks` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_IntakeFormLinks_Persons_PersonId` FOREIGN KEY (`PersonId`) REFERENCES `Persons` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_IntakeFormLinks_Reservations_ReservationId` FOREIGN KEY (`ReservationId`) REFERENCES `Reservations` (`Id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_DoctorExaminationRooms_DoctorId` ON `DoctorExaminationRooms` (`DoctorId`);

CREATE INDEX `IX_DoctorExaminationRooms_ExaminationRoomId` ON `DoctorExaminationRooms` (`ExaminationRoomId`);

CREATE INDEX `IX_ExaminationRooms_HospitalId` ON `ExaminationRooms` (`HospitalId`);

CREATE INDEX `IX_Reservations_DoctorId` ON `Reservations` (`DoctorId`);
CREATE INDEX `IX_IntakeFormLinks_PersonId` ON `IntakeFormLinks` (`PersonId`);
CREATE INDEX `IX_IntakeFormLinks_ReservationId` ON `IntakeFormLinks` (`ReservationId`);
CREATE UNIQUE INDEX `IX_IntakeFormLinks_TokenHash` ON `IntakeFormLinks` (`TokenHash`);

CREATE INDEX `IX_Reservations_ExaminationRoomId` ON `Reservations` (`ExaminationRoomId`);

CREATE INDEX `IX_Reservations_ExaminationTypeId` ON `Reservations` (`ExaminationTypeId`);

CREATE INDEX `IX_Reservations_PersonId` ON `Reservations` (`PersonId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260204000804_AddReservationSystem', '8.0.0');

COMMIT;

