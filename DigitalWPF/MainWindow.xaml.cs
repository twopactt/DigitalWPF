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
using DigitalWPF.DatabaseContext;
using DigitalWPF.Helpers;
using DigitalWPF.Statics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;

namespace DigitalWPF;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private DigitalClinicContext _db = new DigitalClinicContext();
    private MessageHelper _mh = new MessageHelper();

    public MainWindow()
    {
        InitializeComponent();
    }

    private void LogInButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var login = LoginBox.Text;
            var password = PasswordBox.Password;

            var doctor = _db.Users
                .Include(u => u.Role)
                .Where(d => d.Login == login && d.Password == password).FirstOrDefault();

            if (doctor == null)
            {
                _mh.ShowError("Неправильный логин или пароль");
                return;
            }

            if (doctor.Role.Name != "врач" && doctor.Role.Name != "администратор")
            {
                _mh.ShowError("Доступ запрещен");
                return;
            }

            CurrentSession.CurrentDoctor = doctor;

            new HomeWindow(doctor).Show();
            this.Close();
        }
        catch (Exception ex)
        {
            _mh.ShowError(ex.Message);
        }
    }
}