using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace HospitalManagement.Core
{
    // === 1. Моделі даних (Entities / DTOs) ===
    public class Patient
    {
        public int Id { get; set; }
        public string TaxId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime BirthDate { get; set; }
        public string Gender { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string LastModifiedBy { get; set; }
    }

    public class AppointmentDetail
    {
        public int AppointmentId { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string Status { get; set; }
        public decimal Price { get; set; }
        public int PatientId { get; set; }
        public string PatientFullName { get; set; }
        public int DoctorId { get; set; }
        public string DoctorFullName { get; set; }
        public string DepartmentName { get; set; }
        public string Specialization { get; set; }
    }

    // === 2. Інтерфейси репозиторіїв ===
    public interface IPatientRepository
    {
        IEnumerable<Patient> GetAllActive();
        void Upsert(Patient patient, int userId);
        void SoftDelete(int patientId, int userId);
    }

    public interface IAppointmentRepository
    {
        IEnumerable<AppointmentDetail> GetAppointmentDetails();
        int CreateAppointment(int patientId, int doctorId, DateTime date, decimal price, int userId);
        void CancelAppointment(int appointmentId, int userId);
    }

    // === 3. Інтерфейс Unit of Work ===
    public interface IUnitOfWork : IDisposable
    {
        IPatientRepository Patients { get; }
        IAppointmentRepository Appointments { get; }
        void Commit();
        void Rollback();
    }

    // === 4. Реалізація PatientRepository (Використовує View та Stored Procedures) ===
    public class PatientRepository : IPatientRepository
    {
        private readonly SqlConnection _connection;
        private readonly SqlTransaction _transaction;

        public PatientRepository(SqlConnection connection, SqlTransaction transaction)
        {
            _connection = connection;
            _transaction = transaction;
        }

        // Читання даних через VIEW
        public IEnumerable<Patient> GetAllActive()
        {
            var list = new List<Patient>();
            using var cmd = new SqlCommand("SELECT * FROM vw_ActivePatients", _connection, _transaction);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Patient
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    TaxId = reader.IsDBNull(reader.GetOrdinal("TaxId")) ? null : reader.GetString(reader.GetOrdinal("TaxId")),
                    FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                    LastName = reader.GetString(reader.GetOrdinal("LastName")),
                    BirthDate = reader.GetDateTime(reader.GetOrdinal("BirthDate")),
                    Gender = reader.GetString(reader.GetOrdinal("Gender")),
                    PhoneNumber = reader.IsDBNull(reader.GetOrdinal("PhoneNumber")) ? null : reader.GetString(reader.GetOrdinal("PhoneNumber")),
                    Address = reader.IsDBNull(reader.GetOrdinal("Address")) ? null : reader.GetString(reader.GetOrdinal("Address")),
                    UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
                    LastModifiedBy = reader.IsDBNull(reader.GetOrdinal("LastModifiedBy")) ? null : reader.GetString(reader.GetOrdinal("LastModifiedBy"))
                });
            }
            return list;
        }

        // Запис через Збережену Процедуру sp_UpsertPatient
        public void Upsert(Patient patient, int userId)
        {
            using var cmd = new SqlCommand("sp_UpsertPatient", _connection, _transaction);
            cmd.CommandType = CommandType.StoredProcedure;

            var idParam = new SqlParameter("@Id", SqlDbType.Int)
            {
                Direction = ParameterDirection.InputOutput,
                Value = patient.Id == 0 ? DBNull.Value : patient.Id
            };
            cmd.Parameters.Add(idParam);
            cmd.Parameters.AddWithValue("@TaxId", (object)patient.TaxId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FirstName", patient.FirstName);
            cmd.Parameters.AddWithValue("@LastName", patient.LastName);
            cmd.Parameters.AddWithValue("@BirthDate", patient.BirthDate);
            cmd.Parameters.AddWithValue("@Gender", patient.Gender);
            cmd.Parameters.AddWithValue("@PhoneNumber", (object)patient.PhoneNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Address", (object)patient.Address ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserId", userId);

            cmd.ExecuteNonQuery();
            patient.Id = (int)idParam.Value;
        }

        // М'яке видалення (Soft Delete) через Stored Procedure
        public void SoftDelete(int patientId, int userId)
        {
            using var cmd = new SqlCommand("sp_SoftDeletePatient", _connection, _transaction);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@PatientId", patientId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.ExecuteNonQuery();
        }
    }

    // === 5. Реалізація AppointmentRepository ===
    public class AppointmentRepository : IAppointmentRepository
    {
        private readonly SqlConnection _connection;
        private readonly SqlTransaction _transaction;

        public AppointmentRepository(SqlConnection connection, SqlTransaction transaction)
        {
            _connection = connection;
            _transaction = transaction;
        }

        // Читання через VIEW
        public IEnumerable<AppointmentDetail> GetAppointmentDetails()
        {
            var list = new List<AppointmentDetail>();
            using var cmd = new SqlCommand("SELECT * FROM vw_AppointmentDetails WHERE IsDeleted = 0", _connection, _transaction);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new AppointmentDetail
                {
                    AppointmentId = reader.GetInt32(reader.GetOrdinal("AppointmentId")),
                    AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
                    Status = reader.GetString(reader.GetOrdinal("Status")),
                    Price = reader.GetDecimal(reader.GetOrdinal("Price")),
                    PatientId = reader.GetInt32(reader.GetOrdinal("PatientId")),
                    PatientFullName = reader.GetString(reader.GetOrdinal("PatientFullName")),
                    DoctorId = reader.GetInt32(reader.GetOrdinal("DoctorId")),
                    DoctorFullName = reader.GetString(reader.GetOrdinal("DoctorFullName")),
                    DepartmentName = reader.GetString(reader.GetOrdinal("DepartmentName")),
                    Specialization = reader.GetString(reader.GetOrdinal("Specialization"))
                });
            }
            return list;
        }

        // Створення прийому через Stored Procedure
        public int CreateAppointment(int patientId, int doctorId, DateTime date, decimal price, int userId)
        {
            using var cmd = new SqlCommand("sp_CreateAppointment", _connection, _transaction);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@PatientId", patientId);
            cmd.Parameters.AddWithValue("@DoctorId", doctorId);
            cmd.Parameters.AddWithValue("@AppointmentDate", date);
            cmd.Parameters.AddWithValue("@Price", price);
            cmd.Parameters.AddWithValue("@UserId", userId);

            var outId = new SqlParameter("@NewAppointmentId", SqlDbType.Int)
            {
                Direction = ParameterDirection.Output
            };
            cmd.Parameters.Add(outId);

            cmd.ExecuteNonQuery();
            return (int)outId.Value;
        }

        // Скасування/видалення прийому через Stored Procedure
        public void CancelAppointment(int appointmentId, int userId)
        {
            using var cmd = new SqlCommand("sp_CancelAppointment", _connection, _transaction);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@AppointmentId", appointmentId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.ExecuteNonQuery();
        }
    }

    // === 6. Реалізація Unit of Work з підтримкою транзакцій ===
    public class UnitOfWork : IUnitOfWork
    {
        private readonly SqlConnection _connection;
        private SqlTransaction _transaction;
        private bool _disposed;

        public IPatientRepository Patients { get; }
        public IAppointmentRepository Appointments { get; }

        public UnitOfWork(string connectionString)
        {
            _connection = new SqlConnection(connectionString);
            _connection.Open();
            _transaction = _connection.BeginTransaction();

            Patients = new PatientRepository(_connection, _transaction);
            Appointments = new AppointmentRepository(_connection, _transaction);
        }

        public void Commit()
        {
            try
            {
                _transaction?.Commit();
            }
            catch
            {
                Rollback();
                throw;
            }
            finally
            {
                _transaction?.Dispose();
                _transaction = _connection.BeginTransaction();
            }
        }

        public void Rollback()
        {
            _transaction?.Rollback();
            _transaction?.Dispose();
            _transaction = _connection.BeginTransaction();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _transaction?.Dispose();
                _connection?.Dispose();
                _disposed = true;
            }
        }
    }

    // === 7. Точка входу в програму (Тестування роботи) ===
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            string connectionString = "Server=.\\SQLEXPRESS;Database=HospitalDb;Integrated Security=True;TrustServerCertificate=True;";
            int currentUserId = 1;

            Console.WriteLine(" СИСТЕМА УПРАВЛІННЯ ЛІКАРНЕЮ ");
            Console.WriteLine("Підключення до бази даних HospitalDb...\n");

            try
            {
                using (IUnitOfWork uow = new UnitOfWork(connectionString))
                {
                    // Списки імен для генерації різних людей
                    string[] maleFirstNames = { "Олександр", "Максим", "Ярослав", "Артем", "Владислав", "Денис" };
                    string[] maleLastNames = { "Клименко", "Василенко", "Козир", "Левченко", "Дорошенко" };

                    string[] femaleFirstNames = { "Юлія", "Вікторія", "Анастасія", "Дарина", "Катерина" };
                    string[] femaleLastNames = { "Мороз", "Костенко", "Петренко", "Романенко" };

                    bool isFemale = Random.Shared.Next(0, 2) == 1;
                    string firstName = isFemale
                        ? femaleFirstNames[Random.Shared.Next(femaleFirstNames.Length)]
                        : maleFirstNames[Random.Shared.Next(maleFirstNames.Length)];
                    string lastName = isFemale
                        ? femaleLastNames[Random.Shared.Next(femaleLastNames.Length)]
                        : maleLastNames[Random.Shared.Next(maleLastNames.Length)];
                    string gender = isFemale ? "F" : "M";

                    // [1] Додаємо нового унікального пацієнта
                    Console.WriteLine("[1] Додавання нового пацієнта (через Stored Procedure)...");
                    var patient = new Patient
                    {
                        TaxId = Random.Shared.Next(100000000, 999999999).ToString(),
                        FirstName = firstName,
                        LastName = lastName,
                        BirthDate = DateTime.Now.AddYears(-Random.Shared.Next(18, 65)),
                        Gender = gender,
                        PhoneNumber = $"+38067{Random.Shared.Next(1000000, 9999999)}",
                        Address = $"м. Київ, вул. Центральна, {Random.Shared.Next(1, 120)}"
                    };

                    uow.Patients.Upsert(patient, currentUserId);
                    Console.WriteLine($" -> Створено пацієнта: {patient.FirstName} {patient.LastName} (Id = {patient.Id})\n");

                    // [2] Вибираємо випадкового лікаря (від 1 до 6) та випадкову вартість
                    // 1 - Терапевт, 2 - Кардіолог, 3 - Невропатолог, 4 - Педіатр, 5 - Травматолог, 6 - Офтальмолог
                    int randomDoctorId = Random.Shared.Next(1, 7);
                    decimal randomPrice = Random.Shared.Next(350, 750);

                    Console.WriteLine($"[2] Запис пацієнта до випадкового лікаря (DoctorId = {randomDoctorId})...");
                    int appointmentId = uow.Appointments.CreateAppointment(
                        patientId: patient.Id,
                        doctorId: randomDoctorId,
                        date: DateTime.Now.AddDays(Random.Shared.Next(1, 14)),
                        price: randomPrice,
                        userId: currentUserId
                    );
                    Console.WriteLine($" -> Прийом створено з Id = {appointmentId}\n");

                    // [3] Читання списку пацієнтів через View
                    Console.WriteLine("[3] Список активних пацієнтів (через VIEW vw_ActivePatients):");
                    var activePatients = uow.Patients.GetAllActive();
                    foreach (var p in activePatients)
                    {
                        Console.WriteLine($" • [{p.Id}] {p.FirstName} {p.LastName} | Тел: {p.PhoneNumber} | Змінив: {p.LastModifiedBy}");
                    }

                    // [4] Читання деталей прийомів через View (будуть різні пацієнти та різні лікарі!)
                    Console.WriteLine("\n[4] Деталі прийомів (через VIEW vw_AppointmentDetails):");
                    var appointments = uow.Appointments.GetAppointmentDetails();
                    foreach (var a in appointments)
                    {
                        Console.WriteLine($" • Прийом №{a.AppointmentId}: Пацієнт: {a.PatientFullName} ➔ Лікар: {a.DoctorFullName} ({a.Specialization}) | Відділення: {a.DepartmentName} | Ціна: {a.Price} грн");
                    }

                    // Фіксуємо транзакцію
                    uow.Commit();
                    Console.WriteLine("\n Усі операції успішно виконано та збережено в БД (Commit)! ");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[Помилка]: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine("\nНатисніть будь-яку клавішу для завершення...");
            Console.ReadKey();
        }
    }
}