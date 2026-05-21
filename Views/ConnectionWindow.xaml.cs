using System;
using System.Windows;
using System.Windows.Controls;
using OnlineStoreApp.Data;

namespace OnlineStoreApp.Views
{
    public partial class ConnectionWindow : Window
    {
        public ConnectionWindow() => InitializeComponent();

        private void CboAuth_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (PanelSql != null)
                PanelSql.Visibility = CboAuth.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            PanelErr.Visibility = Visibility.Collapsed;
            string server = TxtServer.Text.Trim();
            string db     = TxtDb.Text.Trim();
            if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(db))
            { ShowErr("Заполните поля Сервер и База данных."); return; }

            string cs = CboAuth.SelectedIndex == 1
                ? $"Server={server};Database={db};User Id={TxtLogin.Text.Trim()};Password={PwdSql.Password};TrustServerCertificate=True;"
                : $"Server={server};Database={db};Integrated Security=True;TrustServerCertificate=True;";

            try
            {
                if (!DB.Test(cs)) { ShowErr("Не удалось подключиться. Проверьте параметры."); return; }
                DB.ConnStr = cs;
                DialogResult = true;
                Close();
            }
            catch (Exception ex) { ShowErr(ex.Message); }
        }

        private void ShowErr(string m) { TxtErr.Text = m; PanelErr.Visibility = Visibility.Visible; }
    }
}
