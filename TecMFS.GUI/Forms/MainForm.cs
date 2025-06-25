using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using TecMFS.Common.Models;
using TecMFS.GUI.Services;

namespace TecMFS.GUI.Forms
{
    public partial class MainForm : Form
    {
        private ApiClient _apiClient;
        private List<FileMetadata> _currentFiles;

        // Paneles para cada vista
        private Panel _menuPanel;
        private Panel _contentPanel;
        private Panel _filesPanel;
        private Panel _statusPanel;

        // Controles
        private ListBox _filesList;
        private TextBox _searchBox;
        private Button _downloadButton, _deleteButton;
        private ProgressBar _progressBar;
        private Label _statusLabel;

        public MainForm()
        {
            InitializeComponent();
            _apiClient = new ApiClient();
            _currentFiles = new List<FileMetadata>();
            this.Load += MainForm_Load;
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Configuración básica del formulario
            this.Text = "TEC Media File System";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Panel de menú lateral
            _menuPanel = new Panel
            {
                Width = 150,
                Dock = DockStyle.Left,
                BackColor = SystemColors.ControlLight
            };

            var btnFiles = new Button
            {
                Text = "Archivos",
                Size = new Size(140, 30),
                Location = new Point(5, 10)
            };
            btnFiles.Click += (s, e) => ShowFilesPanel();

            var btnStatus = new Button
            {
                Text = "Estado RAID",
                Size = new Size(140, 30),
                Location = new Point(5, 50)
            };
            btnStatus.Click += (s, e) => ShowStatusPanel();

            _menuPanel.Controls.AddRange(new Control[] { btnFiles, btnStatus });

            // Panel de contenido principal
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill
            };

            // Crear los paneles necesarios
            CreateFilesPanel();
            CreateStatusPanel();

            this.Controls.Add(_contentPanel);
            this.Controls.Add(_menuPanel);

            // Mostrar panel de archivos por defecto
            ShowFilesPanel();

            this.ResumeLayout();
        }

        private void CreateFilesPanel()
        {
            _filesPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Visible = false
            };

            var titleLabel = new Label
            {
                Text = "Gestión de Archivos PDF",
                Font = new Font("Arial", 12, FontStyle.Bold),
                Location = new Point(10, 10),
                AutoSize = true
            };

            // Búsqueda
            var searchLabel = new Label
            {
                Text = "Buscar:",
                Location = new Point(10, 50),
                AutoSize = true
            };

            _searchBox = new TextBox
            {
                Location = new Point(70, 47),
                Size = new Size(200, 20)
            };

            var searchButton = new Button
            {
                Text = "Buscar",
                Location = new Point(280, 46),
                Size = new Size(60, 23)
            };
            searchButton.Click += SearchButton_Click;

            var refreshButton = new Button
            {
                Text = "Actualizar",
                Location = new Point(350, 46),
                Size = new Size(70, 23)
            };
            refreshButton.Click += RefreshButton_Click;

            var uploadButton = new Button
            {
                Text = "Subir PDF",
                Location = new Point(430, 46),
                Size = new Size(70, 23),
                BackColor = Color.LightGreen
            };
            uploadButton.Click += UploadButton_Click;

            // Lista de archivos
            _filesList = new ListBox
            {
                Location = new Point(10, 80),
                Size = new Size(500, 300),
                SelectionMode = SelectionMode.One
            };
            _filesList.SelectedIndexChanged += FilesList_SelectedIndexChanged;
            _filesList.DoubleClick += FilesList_DoubleClick;

            // Botones de acción
            _downloadButton = new Button
            {
                Text = "Descargar",
                Location = new Point(10, 390),
                Size = new Size(80, 25),
                Enabled = false
            };
            _downloadButton.Click += DownloadButton_Click;

            _deleteButton = new Button
            {
                Text = "Eliminar",
                Location = new Point(100, 390),
                Size = new Size(80, 25),
                Enabled = false
            };
            _deleteButton.Click += DeleteButton_Click;

            // Barra de progreso para uploads (inicialmente oculta)
            _progressBar = new ProgressBar
            {
                Location = new Point(10, 425),
                Size = new Size(400, 20),
                Visible = false
            };

            // Label de estado (inicialmente oculto)
            _statusLabel = new Label
            {
                Text = "",
                Location = new Point(10, 450),
                Size = new Size(500, 20),
                Visible = false
            };

            _filesPanel.Controls.AddRange(new Control[] {
                titleLabel, searchLabel, _searchBox, searchButton, refreshButton, uploadButton,
                _filesList, _downloadButton, _deleteButton, _progressBar, _statusLabel
            });

            _contentPanel.Controls.Add(_filesPanel);
        }

        private void CreateUploadPanel()
        {
            // Método eliminado - ya no se necesita
        }

        private void CreateStatusPanel()
        {
            _statusPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Visible = false
            };

            var titleLabel = new Label
            {
                Text = "Estado del Sistema RAID 5",
                Font = new Font("Arial", 12, FontStyle.Bold),
                Location = new Point(10, 10),
                AutoSize = true
            };

            var statusTextBox = new TextBox
            {
                Location = new Point(10, 50),
                Size = new Size(550, 350),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9),
                Text = "Verificando estado del sistema...\r\n\r\nSistema: RAID 5\r\nNodos: 4\r\nTolerancia a fallos: 1 nodo\r\n\r\nEstado: Conectando..."
            };

            var refreshStatusButton = new Button
            {
                Text = "Actualizar Estado",
                Location = new Point(10, 410),
                Size = new Size(120, 30)
            };
            refreshStatusButton.Click += RefreshStatusButton_Click;

            _statusPanel.Controls.AddRange(new Control[] {
                titleLabel, statusTextBox, refreshStatusButton
            });

            _contentPanel.Controls.Add(_statusPanel);
        }

        // Navegación entre paneles
        private void ShowFilesPanel()
        {
            HideAllPanels();
            _filesPanel.Visible = true;
            this.Text = "TEC Media File System - Archivos";
        }

        private void ShowStatusPanel()
        {
            HideAllPanels();
            _statusPanel.Visible = true;
            this.Text = "TEC Media File System - Estado";
        }

        private void HideAllPanels()
        {
            _filesPanel.Visible = false;
            _statusPanel.Visible = false;
        }

        // Event Handlers
        private void MainForm_Load(object sender, EventArgs e)
        {
            RefreshFileList();
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            RefreshFileList();
        }

        private void SearchButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_searchBox.Text))
            {
                RefreshFileList();
            }
            else
            {
                SearchFiles();
            }
        }

        private void FilesList_SelectedIndexChanged(object sender, EventArgs e)
        {
            var hasSelection = _filesList.SelectedIndex >= 0;
            _downloadButton.Enabled = hasSelection;
            _deleteButton.Enabled = hasSelection;
        }

        private void FilesList_DoubleClick(object sender, EventArgs e)
        {
            if (_downloadButton.Enabled)
            {
                DownloadButton_Click(sender, e);
            }
        }

        private void SelectPDFButton_Click(object sender, EventArgs e)
        {
            using var openFileDialog = new OpenFileDialog
            {
                Filter = "Archivos PDF|*.pdf",
                Title = "Seleccionar archivo PDF"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                UploadFile(openFileDialog.FileName);
            }
        }

        private void UploadButton_Click(object sender, EventArgs e)
        {
            using var openFileDialog = new OpenFileDialog
            {
                Filter = "Archivos PDF|*.pdf",
                Title = "Seleccionar archivo PDF para subir"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                UploadFile(openFileDialog.FileName);
            }
        }

        private void DownloadButton_Click(object sender, EventArgs e)
        {
            if (_filesList.SelectedIndex < 0) return;

            var selectedFile = _currentFiles[_filesList.SelectedIndex];

            using var saveFileDialog = new SaveFileDialog
            {
                Filter = "Archivos PDF|*.pdf",
                FileName = selectedFile.FileName
            };

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                DownloadFile(selectedFile.FileName, saveFileDialog.FileName);
            }
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            if (_filesList.SelectedIndex < 0) return;

            var selectedFile = _currentFiles[_filesList.SelectedIndex];
            var result = MessageBox.Show(
                $"¿Está seguro de eliminar '{selectedFile.FileName}'?",
                "Confirmar eliminación",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                DeleteFile(selectedFile.FileName);
            }
        }

        private void RefreshStatusButton_Click(object sender, EventArgs e)
        {
            UpdateSystemStatus();
        }

        // Métodos principales (síncronos para evitar threading issues)
        private async void RefreshFileList()
        {
            try
            {
                _filesList.Items.Clear();
                _filesList.Items.Add("Cargando archivos...");

                var files = await _apiClient.GetFileListAsync();

                _filesList.Items.Clear();

                if (files != null && files.Count > 0)
                {
                    _currentFiles = files;
                    foreach (var file in files)
                    {
                        var displayText = $"{file.FileName} ({FormatFileSize(file.FileSize)}) - {file.UploadDate:dd/MM/yyyy}";
                        _filesList.Items.Add(displayText);
                    }
                }
                else
                {
                    _currentFiles = new List<FileMetadata>();
                    _filesList.Items.Add("No hay archivos en el sistema");
                }
            }
            catch (Exception ex)
            {
                _filesList.Items.Clear();
                _filesList.Items.Add($"Error: {ex.Message}");
            }
        }

        private async void SearchFiles()
        {
            try
            {
                var query = _searchBox.Text.Trim();
                _filesList.Items.Clear();
                _filesList.Items.Add($"Buscando '{query}'...");

                var files = await _apiClient.SearchFilesAsync(query);

                _filesList.Items.Clear();

                if (files != null && files.Count > 0)
                {
                    _currentFiles = files;
                    foreach (var file in files)
                    {
                        var displayText = $"{file.FileName} ({FormatFileSize(file.FileSize)}) - {file.UploadDate:dd/MM/yyyy}";
                        _filesList.Items.Add(displayText);
                    }
                }
                else
                {
                    _currentFiles = new List<FileMetadata>();
                    _filesList.Items.Add($"No se encontraron archivos con '{query}'");
                }
            }
            catch (Exception ex)
            {
                _filesList.Items.Clear();
                _filesList.Items.Add($"Error en búsqueda: {ex.Message}");
            }
        }

        private async void UploadFile(string filePath)
        {
            try
            {
                var fileName = Path.GetFileName(filePath);
                _statusLabel.Text = $"Subiendo {fileName}...";
                _statusLabel.Visible = true;
                _progressBar.Visible = true;
                _progressBar.Value = 0;

                var progress = new Progress<ApiClient.ProgressEventArgs>(p =>
                {
                    _progressBar.Value = (int)p.PercentageComplete;
                    _statusLabel.Text = $"Subiendo {fileName}: {p.PercentageComplete:F1}%";
                });

                var result = await _apiClient.UploadFileAsync(filePath, progress);

                if (result?.Success == true)
                {
                    _statusLabel.Text = $"Archivo {fileName} subido exitosamente";
                    MessageBox.Show($"Archivo subido exitosamente: {fileName}", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    RefreshFileList();
                }
                else
                {
                    _statusLabel.Text = $"Error subiendo {fileName}";
                    MessageBox.Show($"Error subiendo archivo: {result?.Message ?? "Error desconocido"}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Error durante upload";
                _statusLabel.Visible = true;
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _progressBar.Visible = false;
                // Ocultar status después de 3 segundos usando Windows Forms Timer
                var timer = new System.Windows.Forms.Timer { Interval = 3000 };
                timer.Tick += (s, e) => { _statusLabel.Visible = false; timer.Stop(); };
                timer.Start();
            }
        }

        private async void DownloadFile(string fileName, string savePath)
        {
            try
            {
                _statusLabel.Text = $"Descargando {fileName}...";
                _progressBar.Visible = true;
                _progressBar.Value = 0;

                var progress = new Progress<ApiClient.ProgressEventArgs>(p =>
                {
                    _progressBar.Value = (int)p.PercentageComplete;
                    _statusLabel.Text = $"Descargando {fileName}: {p.PercentageComplete:F1}%";
                });

                var result = await _apiClient.DownloadFileAsync(fileName, savePath, progress);

                if (result?.Success == true)
                {
                    _statusLabel.Text = $"Archivo {fileName} descargado exitosamente";

                    var openResult = MessageBox.Show(
                        $"Archivo descargado en:\n{savePath}\n\n¿Desea abrirlo?",
                        "Descarga completada",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (openResult == DialogResult.Yes)
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = savePath,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error abriendo archivo: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
                else
                {
                    _statusLabel.Text = $"Error descargando {fileName}";
                    MessageBox.Show($"Error descargando archivo: {result?.Message ?? "Error desconocido"}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Error durante download";
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _progressBar.Visible = false;
            }
        }

        private async void DeleteFile(string fileName)
        {
            try
            {
                _statusLabel.Text = $"Eliminando {fileName}...";

                var success = await _apiClient.DeleteFileAsync(fileName);

                if (success)
                {
                    _statusLabel.Text = $"Archivo {fileName} eliminado exitosamente";
                    MessageBox.Show($"Archivo eliminado: {fileName}", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    RefreshFileList();
                }
                else
                {
                    _statusLabel.Text = $"Error eliminando {fileName}";
                    MessageBox.Show($"Error eliminando archivo: {fileName}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Error durante eliminación";
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void UpdateSystemStatus()
        {
            try
            {
                var statusTextBox = (TextBox)_statusPanel.Controls[1];
                statusTextBox.Text = "Verificando estado del sistema...";

                var isOnline = await _apiClient.IsControllerAvailableAsync();

                if (isOnline)
                {
                    var status = await _apiClient.GetSystemStatusAsync();
                    var onlineNodes = status?.OnlineNodes ?? 0;

                    statusTextBox.Text = $"Estado del Sistema RAID 5\r\n" +
                                        $"========================\r\n\r\n" +
                                        $"Sistema: Online\r\n" +
                                        $"Nodos activos: {onlineNodes}/4\r\n" +
                                        $"Archivos totales: {status?.TotalFiles ?? 0}\r\n" +
                                        $"Bloques totales: {status?.TotalBlocks ?? 0}\r\n" +
                                        $"Tolerancia a fallos: 1 nodo\r\n\r\n" +
                                        $"Estado del RAID: {(onlineNodes >= 3 ? "Operacional" : "Degradado")}\r\n" +
                                        $"Última verificación: {DateTime.Now:dd/MM/yyyy HH:mm:ss}\r\n\r\n" +
                                        $"Detalles:\r\n" +
                                        $"- RAID Level: 5\r\n" +
                                        $"- Distribución: Striping con paridad\r\n" +
                                        $"- Redundancia: 1 disco de paridad\r\n" +
                                        $"- Protocolo: HTTP/JSON";
                }
                else
                {
                    statusTextBox.Text = $"Estado del Sistema RAID 5\r\n" +
                                        $"========================\r\n\r\n" +
                                        $"Sistema: Offline\r\n" +
                                        $"Error: No se puede conectar al Controller\r\n" +
                                        $"Última verificación: {DateTime.Now:dd/MM/yyyy HH:mm:ss}\r\n\r\n" +
                                        $"Posibles causas:\r\n" +
                                        $"- Controller no está ejecutándose\r\n" +
                                        $"- Problemas de red\r\n" +
                                        $"- Puertos bloqueados\r\n\r\n" +
                                        $"Verificar:\r\n" +
                                        $"- Puerto 5000 disponible\r\n" +
                                        $"- Proceso TecMFS.Controller corriendo";
                }
            }
            catch (Exception ex)
            {
                var statusTextBox = (TextBox)_statusPanel.Controls[1];
                statusTextBox.Text = $"Error verificando estado del sistema:\r\n\r\n{ex.Message}\r\n\r\nÚltima verificación: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
            }
        }

        // Helper methods
        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1048576) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1073741824) return $"{bytes / 1048576.0:F1} MB";
            return $"{bytes / 1073741824.0:F1} GB";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _apiClient?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}