using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MonoTorrent;
using MonoTorrent.Client;
using QWTorrent_GUI;
namespace TorrentWpfClient
{
    public partial class MainWindow : Window
    {
        private ClientEngine engine;
        private List<TorrentManager> torrents = new List<TorrentManager>();
        private ObservableCollection<TorrentStatus> torrentStatusList = new ObservableCollection<TorrentStatus>();
        private string downloadPath = @"C:\path\to\download"; // Значение по умолчанию

        public MainWindow()
        {
            InitializeComponent();
            TorrentStatusList.ItemsSource = torrentStatusList;

            // Создаем настройки торрент-клиента
            var engineSettings = new EngineSettings
            {
                // Настройка других параметров, если необходимо
            };

            // Создаем движок торрент-клиента
            engine = new ClientEngine(engineSettings);
        }

        // Обработчик нажатия на кнопку "Добавить торренты"
        private async void AddTorrentsButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Torrent files (*.torrent)|*.torrent",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string[] torrentFilePaths = openFileDialog.FileNames;

                using (var folderDialog = new System.Windows.Forms.FolderBrowserDialog())
                {
                    folderDialog.Description = "Выберите папку для сохранения файлов";
                    folderDialog.SelectedPath = downloadPath;

                    if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        downloadPath = folderDialog.SelectedPath;

                        foreach (var torrentFilePath in torrentFilePaths)
                        {
                            Torrent torrent;
                            using (var stream = File.OpenRead(torrentFilePath))
                            {
                                torrent = Torrent.Load(stream);
                            }

                            if (torrents.Any(t => t.Torrent == torrent))
                            {
                                MessageBox.Show($"Торрент {torrent.Name} уже был добавлен.");
                                continue;
                            }

                            // Создаем менеджер торрента
                            var manager = await engine.AddAsync(torrent, downloadPath);
                            torrents.Add(manager);

                            // Добавляем торрент в список интерфейса
                            TorrentList.Items.Add(torrent);

                            // Добавляем статус торрента в ObservableCollection
                            var status = new TorrentStatus
                            {
                                Name = torrent.Name,
                                Progress = "0%",
                                Status = "Запуск"
                            };
                            torrentStatusList.Add(status);

                            // Запускаем загрузку сразу после добавления
                            await manager.StartAsync();
                            MonitorProgress(manager);
                        }
                    }
                }
            }
        }

        // Метод для мониторинга прогресса загрузки
        private async void MonitorProgress(TorrentManager manager)
        {
            var statusItem = torrentStatusList
                .FirstOrDefault(item => item.Name == manager.Torrent.Name);

            while (manager.Progress < 100)
            {
                var progress = Math.Round(manager.Progress, 2);
                Dispatcher.Invoke(() =>
                {
                    DownloadProgressBar.Value = progress;
                    ProgressTextBlock.Text = $"{progress}%";

                    if (statusItem != null)
                    {
                        statusItem.Progress = $"{progress}%";
                        statusItem.Status = "Загрузка";
                    }
                });

                await Task.Delay(1000);
            }

            // Устанавливаем полное значение после завершения
            Dispatcher.Invoke(() =>
            {
                DownloadProgressBar.Value = 100;
                ProgressTextBlock.Text = "100%";

                if (statusItem != null)
                {
                    statusItem.Progress = "100%";
                    statusItem.Status = "Завершено";
                }
            });
        }

        // Обработчик нажатия на кнопку "Остановить"
        private async void StopTorrentButton_Click(object sender, RoutedEventArgs e)
        {
            if (TorrentList.SelectedItem is Torrent selectedTorrent)
            {
                var manager = torrents.Find(t => t.Torrent == selectedTorrent);
                if (manager != null)
                {
                    await manager.StopAsync();
                    await WaitForManagerState(manager, TorrentState.Stopped);

                    var statusItem = torrentStatusList
                        .FirstOrDefault(item => item.Name == selectedTorrent.Name);

                    if (statusItem != null)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            statusItem.Status = "Остановлено";
                        });
                    }
                }
            }
        }

        private async Task WaitForManagerState(TorrentManager manager, TorrentState targetState)
        {
            while (manager.State != targetState)
            {
                await Task.Delay(500); // Ожидание полсекунды
            }
        }

        // Обработчик нажатия на кнопку "Запустить"
        private async void StartTorrentButton_Click(object sender, RoutedEventArgs e)
        {
            if (TorrentList.SelectedItem is Torrent selectedTorrent)
            {
                var manager = torrents.Find(t => t.Torrent == selectedTorrent);
                if (manager != null)
                {
                    if (manager.State == TorrentState.Stopped || manager.State == TorrentState.Paused)
                    {
                        await manager.StartAsync();
                        MonitorProgress(manager);
                    }
                    else
                    {
                        MessageBox.Show("Торрент находится в состоянии, в котором нельзя запустить его.");
                    }
                }
            }
        }
    }
}