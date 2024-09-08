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
        private TorrentManager selectedManager;
        private List<TorrentManager> torrents = new List<TorrentManager>();
        private ObservableCollection<TorrentStatus> torrentStatusList = new ObservableCollection<TorrentStatus>();
        private string downloadPath = @"C:\path\to\download";

        public MainWindow()
        {
            InitializeComponent();
            TorrentStatusList.ItemsSource = torrentStatusList;

            var engineSettings = new EngineSettings
            {
                // Engine settings initialization (if necessary)
            };

            engine = new ClientEngine(engineSettings);

            
            TorrentStatusList.SelectionChanged += TorrentStatusList_SelectionChanged;
        }

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
                    folderDialog.Description = "Select a folder to save files";
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
                                MessageBox.Show($"Torrent {torrent.Name} has already been added.");
                                continue;
                            }

                            var manager = await engine.AddAsync(torrent, downloadPath);
                            torrents.Add(manager);

                            TorrentList.Items.Add(torrent);

                            var status = new TorrentStatus
                            {
                                Name = torrent.Name,
                                Progress = "0%",
                                Status = "Starting"
                            };
                            torrentStatusList.Add(status);

                            await manager.StartAsync();
                            MonitorProgress(manager);
                        }
                    }
                }
            }
        }

       
        private void TorrentStatusList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TorrentStatusList.SelectedItem is TorrentStatus selectedStatus)
            {
                selectedManager = torrents.FirstOrDefault(t => t.Torrent.Name == selectedStatus.Name);
                if (selectedManager != null)
                {
                    UpdateProgressBar();
                }
            }
        }

        
        private void UpdateProgressBar()
        {
            if (selectedManager != null)
            {
                Dispatcher.Invoke(() =>
                {
                    DownloadProgressBar.Value = selectedManager.Progress;
                });
            }
        }

        
        private async void MonitorProgress(TorrentManager manager)
        {
            var statusItem = torrentStatusList.FirstOrDefault(item => item.Name == manager.Torrent.Name);

            while (manager.Progress < 100)
            {
                var progress = Math.Round(manager.Progress, 2);
                Dispatcher.Invoke(() =>
                {
                    if (selectedManager == manager)
                    {
                        DownloadProgressBar.Value = progress;
                    }

                    if (statusItem != null)
                    {
                        statusItem.Progress = $"{progress}%";
                        statusItem.Status = "Downloading";
                    }
                });

                await Task.Delay(1000);
            }

            Dispatcher.Invoke(() =>
            {
                if (statusItem != null)
                {
                    statusItem.Progress = "100%";
                    statusItem.Status = "Completed";
                }
            });
        }

        
        private async void DeleteTorrentButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("The program stops downloading this torrent file, please wait");
            if (TorrentStatusList.SelectedItem is TorrentStatus selectedStatus)
            {
                var manager = torrents.FirstOrDefault(t => t.Torrent.Name == selectedStatus.Name);
                if (manager != null)
                {
                    if (manager.State == TorrentState.Seeding || manager.State == TorrentState.Downloading)
                    {
                        await manager.StopAsync();
                        await WaitForManagerState(manager, TorrentState.Stopped);
                    }

                    
                    var result = MessageBox.Show("Delete application folder?",
                        "Delete torrent", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            
                            string torrentDownloadPath = Path.Combine(downloadPath, selectedStatus.Name);

                            if (Directory.Exists(torrentDownloadPath))
                            {
                                Directory.Delete(torrentDownloadPath, true);
                                MessageBox.Show($"The application folder {torrentDownloadPath} has been deleted.");
                            }
                            
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to delete folder: {ex.Message}");
                        }
                    }

                    await engine.RemoveAsync(manager);

                    torrents.Remove(manager);

                    torrentStatusList.Remove(selectedStatus);

                    if (selectedManager == manager)
                    {
                        selectedManager = null;
                        DownloadProgressBar.Value = 0;
                    }
                }
            }
        }

        private async Task WaitForManagerState(TorrentManager manager, TorrentState targetState)
        {
            while (manager.State != targetState)
            {
                await Task.Delay(500);
            }
        }
    }
}
