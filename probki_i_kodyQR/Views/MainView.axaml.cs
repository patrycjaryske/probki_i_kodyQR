using Avalonia.Controls;
using Avalonia.Interactivity;
using QRCoder.Core;
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using System.Drawing;

using System.Drawing.Imaging;
using System.Collections.ObjectModel;
using System.Linq;



namespace probki_i_kodyQR.Views;


public partial class MainView : UserControl
{
    private string _dbPath = "samples.db";
    private List<Sample> _samples = new();
    private int? _editingSampleId = null;
    private Sample? _currentSample = null;


    public MainView()
    {
        InitializeComponent();
        InitDatabase();
        LoadSamples();
        SearchBox.TextChanged += SearchBox_TextChanged;
        FilterDNA.Checked += Filter_Changed;
        FilterDNA.Unchecked += Filter_Changed;
        FilterRNA.Checked += Filter_Changed;
        FilterRNA.Unchecked += Filter_Changed;
        FilterProtein.Checked += Filter_Changed;
        FilterProtein.Unchecked += Filter_Changed;
        FilterOther.Checked += Filter_Changed;
        FilterOther.Unchecked += Filter_Changed;

        FilterDateFrom.SelectedDateChanged += Filter_Changed;
        FilterDateTo.SelectedDateChanged += Filter_Changed;

    }
    private void ClearDate_Click(object? sender, RoutedEventArgs e)
    {
        FilterDateFrom.SelectedDate = null;
        FilterDateTo.SelectedDate = null;

    }



    private void InitDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Samples (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Type TEXT NOT NULL,
                CollectionDate TEXT NOT NULL,
                Notes TEXT
            );
        """;
        cmd.ExecuteNonQuery();
    }

    private void LoadSamples()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Samples;";
        using var reader = cmd.ExecuteReader();

        _samples.Clear();
        while (reader.Read())
        {
            _samples.Add(new Sample
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Type = reader.GetString(2),
                CollectionDate = DateTime.Parse(reader.GetString(3)),
                Notes = reader.IsDBNull(4) ? "" : reader.GetString(4)
            });
        }

        SampleList.ItemsSource = null;
        SampleList.ItemsSource = _samples;

    }


    private void SaveSample_Click(object? sender, RoutedEventArgs e)
    {
        var name = NameBox.Text ?? "";
        var type = TypeBox.SelectedItem?.ToString() ?? "Inny";
        var date = DateBox.SelectedDate ?? DateTime.Now;
        var notes = NotesBox.Text ?? "";

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        var cmd = connection.CreateCommand();

        if (_currentSample == null)
        {
            // Dodaj nową próbkę
            cmd.CommandText = "INSERT INTO Samples (Name, Type, CollectionDate, Notes) VALUES ($name, $type, $date, $notes)";
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$type", type);
            cmd.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("$notes", notes);
            cmd.ExecuteNonQuery();
        }
        else
        {
            // Aktualizuj istniejącą próbkę
            cmd.CommandText = @"UPDATE Samples 
                            SET Name = $name, Type = $type, CollectionDate = $date, Notes = $notes 
                            WHERE Id = $id";
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$type", type);
            cmd.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("$notes", notes);
            cmd.Parameters.AddWithValue("$id", _currentSample.Id);
            cmd.ExecuteNonQuery();
        }

        LoadSamples();
        _currentSample = null;
        ClearForm();
    }


    private void ClearForm()
    {
        NameBox.Text = "";
        TypeBox.SelectedIndex = 0;
        DateBox.SelectedDate = null;
        NotesBox.Text = "";
    }

    private void SampleList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SampleList.SelectedItem is Sample sample)
        {
            _currentSample = sample;
            SampleIdTextBox.Text = sample.Id.ToString();

            NameBox.Text = sample.Name;

            var matchingType = TypeBox.Items.Cast<string>()
                              .FirstOrDefault(t => string.Equals(t, sample.Type, StringComparison.OrdinalIgnoreCase));
            if (matchingType != null)
                TypeBox.SelectedItem = matchingType;
            else
                TypeBox.SelectedIndex = 0;

            DateBox.SelectedDate = sample.CollectionDate;
            NotesBox.Text = sample.Notes;
        }
        else
        {
            _currentSample = null;
            SampleIdTextBox.Text = ""; // Czyścimy jeśli nic nie wybrano

        }
    }



    private void SearchBox_TextChanged(object? sender, RoutedEventArgs e)
    {
        ApplyFilters();

        string query = SearchBox.Text?.Trim().ToLower() ?? "";

        var filtered = string.IsNullOrWhiteSpace(query)
            ? _samples
            : _samples.FindAll(s =>
                s.Name.ToLower().Contains(query) ||
                s.Type.ToLower().Contains(query) ||
                s.Notes.ToLower().Contains(query));

        SampleList.ItemsSource = null;
        SampleList.ItemsSource = filtered;

    }
    private void GenerateQrCode_Click(object? sender, RoutedEventArgs e)
    {
        if (SampleList.SelectedItem is not Sample sample)
        {
            return; // lub pokaż komunikat
        }

        string qrData = $"ID: {sample.Id}\nNazwa: {sample.Name}\nTyp: {sample.Type}\nData: {sample.CollectionDate:yyyy-MM-dd}\nUwagi: {sample.Notes}";

        using var qrGenerator = new QRCodeGenerator();
        using var qrDataObj = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new QRCode(qrDataObj);
        using var bitmap = qrCode.GetGraphic(20);
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        ms.Seek(0, SeekOrigin.Begin);

        // Konwersja System.Drawing.Bitmap -> Avalonia.Controls.Image
        QrImage.Source = new Avalonia.Media.Imaging.Bitmap(ms);

        QrNameTextBlock.Text = sample.Name;

    }
    private async void SaveQrCode_Click(object? sender, RoutedEventArgs e)
    {
        if (SampleList.SelectedItem is not Sample sample)
            return;

        string qrData = $"ID: {sample.Id}\nNazwa: {sample.Name}\nTyp: {sample.Type}\nData: {sample.CollectionDate:yyyy-MM-dd}\nUwagi: {sample.Notes}";

        using var qrGenerator = new QRCodeGenerator();
        using var qrDataObj = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new QRCode(qrDataObj);
        using var bitmap = qrCode.GetGraphic(20);

        var dialog = new SaveFileDialog
        {
            Title = "Zapisz kod QR",
            Filters = new List<FileDialogFilter>
        {
            new FileDialogFilter { Name = "Plik PNG", Extensions = { "png" } }
        },
            DefaultExtension = "png",
            InitialFileName = $"QR_Sample_{sample.Id}.png"
        };

        var filePath = await dialog.ShowAsync((Window)this.VisualRoot!);

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            bitmap.Save(filePath, ImageFormat.Png);
        }
    }
    private void Filter_Changed(object? sender, RoutedEventArgs e)
    {
        ApplyFilters();
    }

    private void Filter_Changed(object? sender, DatePickerSelectedValueChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        string query = SearchBox.Text?.Trim().ToLower() ?? "";

        // Zbierz aktywne typy próbek
        var activeTypes = new List<string>();
        if (FilterDNA.IsChecked == true) activeTypes.Add("DNA");
        if (FilterRNA.IsChecked == true) activeTypes.Add("RNA");
        if (FilterProtein.IsChecked == true) activeTypes.Add("Białko");
        if (FilterOther.IsChecked == true) activeTypes.Add("Inny");

        DateTime? dateFrom = FilterDateFrom.SelectedDate?.DateTime;
        DateTime? dateTo = FilterDateTo.SelectedDate?.DateTime;


        var filtered = _samples.Where(s =>
            // Filtr wg wyszukiwania tekstowego
            (string.IsNullOrWhiteSpace(query) ||
             s.Name.ToLower().Contains(query) ||
             s.Type.ToLower().Contains(query) ||
             s.Notes.ToLower().Contains(query))

            // Filtr wg typu (jeśli aktywne jakieś checkboxy)
            && (activeTypes.Count == 0 || activeTypes.Contains(s.Type))

            // Filtr wg daty
            && (!dateFrom.HasValue || s.CollectionDate >= dateFrom.Value)
            && (!dateTo.HasValue || s.CollectionDate <= dateTo.Value)
        ).ToList();

        SampleList.ItemsSource = null;
        SampleList.ItemsSource = filtered;
    }
}






public class Sample
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public DateTime CollectionDate { get; set; }
    public string Notes { get; set; }

   
}