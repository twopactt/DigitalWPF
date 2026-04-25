using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DigitalWPF.DatabaseContext;
using DigitalWPF.Helpers;
using DigitalWPF.Statics;
using DigitalWPF.Models;
using Microsoft.EntityFrameworkCore;
using System.Drawing;
using System.IO;

namespace DigitalWPF;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class HomeWindow : Window
{
    private DigitalClinicContext _db = new DigitalClinicContext();
    private MessageHelper _mh = new MessageHelper();

    private User _currentDoctor;

    public HomeWindow(User doctor)
    {
        InitializeComponent();

        _currentDoctor = doctor;

        LoadDoctorInfo();
        LoadPatients();
    }

    private void LoadDoctorInfo()
    {
        try
        {
            var doctorWithDepartment = _db.Users
                .Include(u => u.Department)
                .Include(u => u.Role)
                .FirstOrDefault(d => d.Id == _currentDoctor.Id);

            if (doctorWithDepartment != null)
            {
                string fullName = $"{doctorWithDepartment.Surname} " +
                                    $"{doctorWithDepartment.Name} " + 
                                    $"{doctorWithDepartment.Patronymic}"
                                    .Trim();

                DoctorFullNameText.Text = fullName;
                DoctorDepartmentText.Text = doctorWithDepartment.Department.Name;
                UserRoleText.Text = doctorWithDepartment.Role.Name;
            }
        }
        catch (Exception ex)
        {
            _mh.ShowError(ex.Message);
        }
    }

    private void LoadPatients()
    {
        try
        {
            var today = DateTime.Today;
    
            var patientsData = _db.Patients
                .Include(p => p.User)
                .Include(p => p.Measurements)
                .Include(p => p.Prescriptions)
                    .ThenInclude(pr => pr.PrescriptionStatus)
                .Include(p => p.PatientStatus)
                .Include(p => p.TwinStates)
                .Select(p => new
                {
                    p.Id,
                    UserId = p.User.Id,
                    Surname = p.User.Surname,
                    Name = p.User.Name,
                    Patronymic = p.User.Patronymic,
                    FullName = $"{p.User.Surname} {p.User.Name} {p.User.Patronymic ?? ""}".Trim(),
                    p.Birthday,
                    Age = DateTime.Now.Year - p.Birthday.Year,
                    LastMeasurement = p.Measurements.Any()
                        ? p.Measurements.OrderByDescending(m => m.MeasuredAt).First().MeasuredAt
                        : (DateTime?)null,
                    ActivePrescriptionsCount = p.Prescriptions.Any()
                        ? p.Prescriptions.Count(pr => pr.PrescriptionStatus.Name == "активно" || pr.PrescriptionStatusId == 1)
                        : 0,
                    Status = p.PatientStatus.Name,
                    StatusId = p.PatientStatusId,
                    CurrentRisk = p.TwinStates.Any()
                        ? p.TwinStates.OrderByDescending(ts => ts.CalculatedAt).First().RiskIndex
                        : (int?)null,
                    RiskLevel = p.TwinStates.Any() 
                        ? (p.TwinStates.OrderByDescending(ts => ts.CalculatedAt).First().RiskIndex <= 35 ? "Low"
                        : p.TwinStates.OrderByDescending(ts => ts.CalculatedAt).First().RiskIndex <= 70 ? "Medium" : "High")
                        : "Low"
                })
                .ToList();

                PatientsGrid.ItemsSource = patientsData;
        }
        catch (Exception ex)
        {
            _mh.ShowError(ex.Message);
        }
    }

    private void OpenPatient_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var button = sender as Button;
            if (button?.Tag == null) return;

            var patientId = (int)button.Tag;
            
            new PatientMonitoringWindow(patientId, _currentDoctor).ShowDialog();

            LoadPatients();
        }
        catch (Exception ex)
        {
            _mh.ShowError(ex.Message);
        }
    }

    // Отчёт 1: Пациенты
    private void ExportPatientsToCsv_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var patients = _db.Patients
                .Include(p => p.User)
                .Include(p => p.TwinStates)
                .Select(p => new
                {
                    ФИО = (p.User.Surname ?? "") + " " + (p.User.Name ?? "") + " " + (p.User.Patronymic ?? ""),
                    Возраст = DateTime.Now.Year - p.Birthday.Year,
                    Риск = p.TwinStates.OrderByDescending(ts => ts.CalculatedAt).FirstOrDefault() != null 
                        ? p.TwinStates.OrderByDescending(ts => ts.CalculatedAt).First().RiskIndex.ToString() 
                        : "Нет данных",  // ← если нет TwinState
                    Статус = p.PatientStatus.Name ?? ""
                })
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("ФИО;Возраст;Риск;Статус");

            foreach (var p in patients)
            {
                sb.AppendLine($"{p.ФИО};{p.Возраст};{p.Риск};{p.Статус}");
            }

            var filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"Отчёт_Пациенты_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);

            _mh.ShowInfo($"Отчёт сохранён: {filePath}");
        }
        catch (Exception ex)
        {
            _mh.ShowError($"Ошибка: {ex.Message}");
        }
    }

    // Отчёт 2: Назначения
    private void ExportPrescriptionsToCsv_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var prescriptions = _db.Prescriptions
                .Include(p => p.Patient)
                    .ThenInclude(pa => pa.User)
                .Include(p => p.PrescriptionStatus)
                .Select(p => new
                {
                    Пациент = p.Patient.User.Surname + " " + p.Patient.User.Name,
                    Действие = p.ActionDescription ?? (p.Medication != null ? p.Medication.Name : ""),
                    Статус = p.PrescriptionStatus.Name,
                    ДатаНазначения = p.CreatedAt
                })
                .ToList();
            var sb = new StringBuilder();
            sb.AppendLine("Пациент;Действие;Статус;Дата назначения");
            foreach (var pr in prescriptions)
            {
                sb.AppendLine($"{pr.Пациент};{pr.Действие};{pr.Статус};{pr.ДатаНазначения:dd.MM.yyyy}");
            }
            var filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"Отчёт_Назначения_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            _mh.ShowInfo($"Отчёт сохранён: {filePath}");
        }
        catch (Exception ex)
        {
            _mh.ShowError($"Ошибка: {ex.Message}");
        }
    }

    // Отчёт 3: События за период
    private void ExportEventsToCsv_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var startDate = StartDatePicker.SelectedDate ?? DateTime.Now.AddDays(-30);
            var endDate = EndDatePicker.SelectedDate ?? DateTime.Now;
            
            // Замеры
            var measurements = _db.Measurements
                .Where(m => m.MeasuredAt >= startDate && m.MeasuredAt <= endDate)
                .Select(m => new
                {
                    Дата = m.MeasuredAt,
                    Тип = "Замер",
                    Описание = $"Давление: {m.SystolicPressure}/{m.DiastolicPressure}, Пульс: {m.HeartRate}"
                })
                .ToList();
            
            // Назначения
            var prescriptions = _db.Prescriptions
                .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate)
                .Select(p => new
                {
                    Дата = p.CreatedAt,
                    Тип = "Назначение",
                    Описание = p.ActionDescription ?? (p.Medication != null ? p.Medication.Name : "")
                })
                .ToList();
            
            // События (из аудита)
            var events = _db.AuditLogs
                .Where(a => a.CreatedAt >= startDate && a.CreatedAt <= endDate)
                .Select(a => new
                {
                    Дата = a.CreatedAt,
                    Тип = a.ActionType,
                    Описание = a.NewValue
                })
                .ToList();
            
            var allEvents = measurements.Cast<object>()
                .Concat(prescriptions)
                .Concat(events)
                .OrderBy(e => e.GetType().GetProperty("Дата")?.GetValue(e))
                .ToList();
            
            var sb = new StringBuilder();
            sb.AppendLine("Дата;Тип;Описание");
            
            foreach (var ev in allEvents)
            {
                var type = ev.GetType();
                var date = type.GetProperty("Дата")?.GetValue(ev);
                var eventType = type.GetProperty("Тип")?.GetValue(ev);
                var description = type.GetProperty("Описание")?.GetValue(ev);
                
                sb.AppendLine($"{date:dd.MM.yyyy HH:mm};{eventType};{description}");
            }
            
            var filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"Отчёт_События_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            
            _mh.ShowInfo($"Отчёт сохранён: {filePath}");
        }
        catch (Exception ex)
        {
            _mh.ShowError($"Ошибка: {ex.Message}");
        }
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        CurrentSession.CurrentDoctor = null;
        new MainWindow().Show();
        this.Close();
    }
}