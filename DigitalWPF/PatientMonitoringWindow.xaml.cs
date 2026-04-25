using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DigitalWPF.DatabaseContext;
using DigitalWPF.Models;
using Microsoft.EntityFrameworkCore;
using DigitalWPF.Helpers;

namespace DigitalWPF
{
    public partial class PatientMonitoringWindow : Window
    {
        private readonly DigitalClinicContext _db = new DigitalClinicContext();
        private readonly int _patientId;
        private readonly User _currentDoctor;
        private readonly MessageHelper _mh = new MessageHelper();

        public PatientMonitoringWindow(int patientId, User doctor)
        {
            InitializeComponent();
            _patientId = patientId;
            _currentDoctor = doctor;
            LoadAllData();
        }

        private void LoadAllData()
        {
            try
            {
                var patient = _db.Patients
                    .Include(p => p.User)
                    .Include(p => p.PatientStatus)
                    .FirstOrDefault(p => p.Id == _patientId);

                if (patient == null)
                {
                    _mh.ShowError("Пациент не найден");
                    Close();
                    return;
                }

                // Общая информация
                PatientNameText.Text = $"{patient.User.Surname} {patient.User.Name} {patient.User.Patronymic ?? ""}".Trim();
                PatientInfoText.Text = $"Возраст: {DateTime.Now.Year - patient.Birthday.Year} лет | Статус: {patient.PatientStatus.Name}";

                // Хронические заболевания
                var chronicDiseases = _db.PatientChronicDiseases
                    .Include(pcd => pcd.ChronicDisease)
                    .Where(pcd => pcd.PatientId == _patientId)
                    .Select(pcd => pcd.ChronicDisease.Name)
                    .ToList();
                
                ChronicDiseasesList.ItemsSource = chronicDiseases;

                // Текущий риск
                var lastTwinState = _db.TwinStates
                    .Where(ts => ts.PatientId == _patientId)
                    .OrderByDescending(ts => ts.CalculatedAt)
                    .FirstOrDefault();

                if (lastTwinState != null)
                {
                    var risk = lastTwinState.RiskIndex;
                    RiskText.Text = risk.ToString();
                    
                    if (risk <= 35)
                        RiskBorder.Background = new SolidColorBrush(Colors.LightGreen);
                    else if (risk <= 70)
                        RiskBorder.Background = new SolidColorBrush(Colors.Orange);
                    else
                        RiskBorder.Background = new SolidColorBrush(Colors.LightCoral);
                }

                // Последний замер
                var lastMeasurement = _db.Measurements
                    .Include(m => m.MeasurementComplaints)
                        .ThenInclude(mc => mc.Complaint)
                    .Where(m => m.PatientId == _patientId)
                    .OrderByDescending(m => m.MeasuredAt)
                    .FirstOrDefault();

                if (lastMeasurement != null)
                {
                    LastMeasurementDate.Text = $"Дата: {lastMeasurement.MeasuredAt:dd.MM.yyyy HH:mm}";
                    LastMeasurementValues.Text = $"Давление: {lastMeasurement.SystolicPressure}/{lastMeasurement.DiastolicPressure} | Пульс: {lastMeasurement.HeartRate}";
                    LastMeasurementComplaints.Text = $"Жалобы: {string.Join(", ", lastMeasurement.MeasurementComplaints.Select(mc => mc.Complaint.Name))}";
                }

                // Замеры
                LoadMeasurements();
                
                // Назначения
                LoadPrescriptions();
                
                // Уведомления
                LoadNotifications();
                
                // Аудит
                LoadAudit();
            }
            catch (Exception ex)
            {
                _mh.ShowError(ex.Message);
            }
        }

        private void LoadMeasurements()
        {
            var measurements = _db.Measurements
                .Include(m => m.MeasurementComplaints)
                    .ThenInclude(mc => mc.Complaint)
                .Where(m => m.PatientId == _patientId)
                .OrderByDescending(m => m.MeasuredAt)
                .Select(m => new
                {
                    m.MeasuredAt,
                    Pressure = $"{m.SystolicPressure}/{m.DiastolicPressure}",
                    m.HeartRate,
                    Complaints = string.Join(", ", m.MeasurementComplaints.Select(mc => mc.Complaint.Name))
                })
                .ToList();
            
            MeasurementsGrid.ItemsSource = measurements;
        }

        private void LoadPrescriptions()
        {
            // Активные назначения
            var activePrescriptions = _db.Prescriptions
                .Include(p => p.PrescriptionStatus)
                .Where(p => p.PatientId == _patientId && p.PrescriptionStatusId == 1)
                .Select(p => new
                {
                    p.Id,
                    ActionDescription = p.ActionDescription ?? (p.Medication != null ? p.Medication.Name : ""),
                    StartDate = p.StartDate,
                    Status = p.PrescriptionStatus.Name
                })
                .ToList();

            ActivePrescriptionsGrid.ItemsSource = activePrescriptions;

            // Завершённые назначения
            var completedPrescriptions = _db.Prescriptions
                .Include(p => p.PrescriptionStatus)
                .Where(p => p.PatientId == _patientId && p.PrescriptionStatusId != 1)
                .Select(p => new
                {
                    ActionDescription = p.ActionDescription ?? (p.Medication != null ? p.Medication.Name : ""),
                    Status = p.PrescriptionStatus.Name
                })
                .ToList();

            CompletedPrescriptionsGrid.ItemsSource = completedPrescriptions;
        }

        private void LoadNotifications()
        {
            try
            {
                var notifications = _db.Notifications
                    .Include(n => n.NotificationType)
                    .Where(n => n.PatientId == _patientId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Select(n => new
                    {
                        n.CreatedAt,
                        NotificationType = n.NotificationType != null ? n.NotificationType.Name : "Неизвестный тип",
                        n.Message,
                        n.IsRead
                    })
                    .ToList();  // ← обязательно ToList()

                NotificationsGrid.ItemsSource = notifications;

                // Если данных нет, показываем пустой список без ошибки
                if (notifications.Count == 0)
                {
                    NotificationsGrid.ItemsSource = new System.Collections.Generic.List<object>();
                }
            }
            catch (Exception ex)
            {
                _mh.ShowError($"Ошибка загрузки уведомлений: {ex.Message}");
                NotificationsGrid.ItemsSource = new System.Collections.Generic.List<object>();
            }
        }

        private void LoadAudit()
        {
            var auditLogs = _db.AuditLogs
                .Include(a => a.User)
                .Where(a => a.EntityType == "Patient" && a.EntityId == _patientId)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new
                {
                    a.CreatedAt,
                    a.ActionType,
                    a.NewValue,
                    UserName = a.User.Login
                })
                .ToList();
            
            AuditGrid.ItemsSource = auditLogs;
        }

       private void AddMeasurement_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "Добавить замер",
            Width = 300,
            Height = 250,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };

        var systolicBox = new TextBox { Margin = new Thickness(0, 5, 0, 10) };
        var diastolicBox = new TextBox { Margin = new Thickness(0, 5, 0, 10) };
        var heartRateBox = new TextBox { Margin = new Thickness(0, 5, 0, 10) };

        var content = new StackPanel
        {
            Margin = new Thickness(10),
            Children =
            {
                new TextBlock { Text = "Систола:", Margin = new Thickness(0, 5, 0, 0) },
                systolicBox,
                new TextBlock { Text = "Диастола:" },
                diastolicBox,
                new TextBlock { Text = "Пульс:" },
                heartRateBox,
                new Button { Content = "Сохранить", Height = 30, Margin = new Thickness(0, 10, 0, 0) }
            }
        };

        var saveButton = content.Children.OfType<Button>().First();
        saveButton.Click += (s, args) =>
        {
            try
            {
                var systolic = int.Parse(systolicBox.Text);
                var diastolic = int.Parse(diastolicBox.Text);
                var heartRate = int.Parse(heartRateBox.Text);

                var measurement = new Measurement
                {
                    PatientId = _patientId,
                    MeasuredAt = DateTime.Now,
                    SystolicPressure = systolic,
                    DiastolicPressure = diastolic,
                    HeartRate = heartRate,
                    IsOffline = false
                };
                _db.Measurements.Add(measurement);
                _db.SaveChanges();

                dialog.Close();
                LoadAllData();
                _mh.ShowInfo("Замер добавлен");
            }
            catch (FormatException)
            {
                _mh.ShowError("Введите корректные числовые значения");
            }
        };

        dialog.Content = content;
        dialog.ShowDialog();
    }

        private void CreatePrescription_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Window
            {
                Title = "Создать назначение",
                Width = 350,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };
            
            var actionBox = new TextBox { Margin = new Thickness(0, 5, 0, 10), Height = 60, TextWrapping = TextWrapping.Wrap };
            var startDatePicker = new DatePicker { SelectedDate = DateTime.Now, Margin = new Thickness(0, 5, 0, 10) };
            
            var content = new StackPanel
            {
                Margin = new Thickness(10),
                Children =
                {
                    new TextBlock { Text = "Действие (что нужно сделать):", Margin = new Thickness(0, 5, 0, 0), FontWeight = FontWeights.Bold },
                    actionBox,
                    new TextBlock { Text = "Дата начала:", Margin = new Thickness(0, 5, 0, 0) },
                    startDatePicker,
                    new Button { Content = "Сохранить", Height = 30, Margin = new Thickness(0, 15, 0, 0) }
                }
            };
            
            var saveButton = content.Children.OfType<Button>().First();
            saveButton.Click += (s, args) =>
            {
                try
                {
                    var actionDescription = actionBox.Text;
                    
                    if (string.IsNullOrEmpty(actionDescription))
                    {
                        _mh.ShowError("Введите описание действия");
                        return;
                    }
                    
                    // Создаём назначение (без лекарства, только действие)
                    var prescription = new Prescription
                    {
                        PatientId = _patientId,
                        DoctorId = _currentDoctor.Id,
                        ActionDescription = actionDescription,
                        StartDate = DateOnly.FromDateTime(startDatePicker.SelectedDate ?? DateTime.Now),
                        PrescriptionStatusId = 1,
                        CreatedAt = DateTime.Now
                    };
                    _db.Prescriptions.Add(prescription);
                    _db.SaveChanges();
                    
                    dialog.Close();
                    LoadPrescriptions();
                    _mh.ShowInfo("Назначение создано");
                }
                catch (Exception ex)
                {
                    _mh.ShowError(ex.Message);
                }
            };
            
            dialog.Content = content;
            dialog.ShowDialog();
        }

        private void SaveRecommendation_Click(object sender, RoutedEventArgs e)
        {
            var selected = RecommendationCombo.SelectedItem as ComboBoxItem;
            if (selected != null)
            {
                _db.AuditLogs.Add(new AuditLog
                {
                    UserId = _currentDoctor.Id,
                    ActionType = "doctor_recommendation",
                    EntityType = "Patient",
                    EntityId = _patientId,
                    NewValue = selected.Content.ToString(),
                    CreatedAt = DateTime.Now
                });
                _db.SaveChanges();
                _mh.ShowInfo("Рекомендация сохранена");
            }
        }

        private void PeriodCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        private void ParameterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
    }
}
