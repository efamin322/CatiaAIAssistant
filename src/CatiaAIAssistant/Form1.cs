using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CatiaAIAssistant.Core.Abstractions;
using CatiaAIAssistant.Core.Logging;
using CatiaAIAssistant.Core.Text;
using CatiaAIAssistant.Diagnostics;
using CatiaAIAssistant.Engineering;
using CatiaAIAssistant.UI.Dialogs;

namespace CatiaAIAssistant
{
    public partial class Form1 : Form
    {
        private readonly Color _navColor = Color.FromArgb(22, 36, 56);
        private readonly Color _accentColor = Color.FromArgb(36, 105, 180);
        private readonly Color _successColor = Color.FromArgb(22, 120, 62);
        private readonly Color _backgroundColor = Color.FromArgb(242, 245, 249);
        private readonly Color _textColor = Color.FromArgb(35, 48, 66);

        private readonly IOperationLogger _logger;
        private readonly ICadConnectionService _connectionService;
        private readonly ICadDocumentService _documentManager;
        private readonly IBasicPartService _partService;
        private readonly IEngineeringIntentAnalyzer _intentEngine;
        private readonly IDesignExecutionEngine _mechanicalEngine;
        private readonly Phase1SelfTestRunner _selfTestRunner;

        private FlowLayoutPanel _workspace = null!;
        private Label _pageTitle = null!;
        private Label _pageSubtitle = null!;
        private Label _connectionStatus = null!;
        private Button _connectionButton = null!;
        private RichTextBox _logBox = null!;
        private TextBox _commandBox = null!;
        private RichTextBox _analysisReportBox = null!;

        private EngineeringDesignSpecification? _currentSpecification;
        private string _lastSaveDirectory;
        private readonly List<Panel> _pageCards = new();
        private string _currentPage = "home";

        /// <summary>All dependencies are injected by the Composition Root (<see cref="AppComposition"/>); the form never creates services.</summary>
        public Form1(AppServices services)
        {
            ArgumentNullException.ThrowIfNull(services);
            _logger = services.Logger;
            _connectionService = services.Connection;
            _documentManager = services.Documents;
            _partService = services.Parts;
            _intentEngine = services.IntentAnalyzer;
            _mechanicalEngine = services.DesignEngine;
            _selfTestRunner = services.SelfTests;

            _lastSaveDirectory = _logger.PartsOutputDirectory;
            _logger.EntryAdded += OnLoggerEntryAdded;

            BuildInterface();
            ShowPage("home");

            _logger.Info("CATIA AI Assistant آماده است (مجهز به موتور فهم مهندسی، محاسبات، ساخت قطعات مکانیکی و اسمبلی).", "Startup");
        }

        private void OnLoggerEntryAdded(OperationLogEntry entry)
        {
            if (_logBox == null || _logBox.IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<OperationLogEntry>(OnLoggerEntryAdded), entry);
                return;
            }

            _logBox.AppendText(entry.ToDisplayLine() + "\r\n");
            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.ScrollToCaret();
        }

        private void BuildInterface()
        {
            Text = "CATIA AI Assistant — Intelligent Mechanical Design & CAD Automation";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1280, 860);
            MinimumSize = new Size(1020, 680);
            BackColor = _backgroundColor;
            Font = new Font("Segoe UI", 9.5f);
            RightToLeft = RightToLeft.Yes;
            AutoScaleMode = AutoScaleMode.Dpi;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = _backgroundColor,
                RightToLeft = RightToLeft.No
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 236));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            root.Controls.Add(BuildSidebar(), 0, 0);
            root.Controls.Add(BuildMainPanel(), 1, 0);
            Controls.Add(root);
        }

        private Panel BuildSidebar()
        {
            var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = _navColor, Padding = Padding.Empty };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                BackColor = _navColor
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));

            var brand = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15, 15, 12, 8), BackColor = _navColor };
            var brandTitle = new Label
            {
                Text = "CATIA AI",
                Dock = DockStyle.Top,
                Height = 38,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight
            };
            var brandSubtitle = new Label
            {
                Text = "ENGINEERING ASSISTANT",
                Dock = DockStyle.Top,
                Height = 24,
                ForeColor = Color.FromArgb(169, 190, 214),
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight
            };
            brand.Controls.Add(brandSubtitle);
            brand.Controls.Add(brandTitle);

            var nav = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(10, 8, 10, 8),
                BackColor = _navColor
            };

            nav.Controls.Add(CreateNavButton("⌂   داشبورد مهندسی", "home"));
            nav.Controls.Add(CreateNavButton("✧   AI Assistant و تحلیل", "ai"));
            nav.Controls.Add(CreateNavButton("✎   Sketch و ترسیم", "sketch"));
            nav.Controls.Add(CreateNavButton("▣   Part و اجزای ماشین", "part"));
            nav.Controls.Add(CreateNavButton("▦   Assembly و مکانیزم‌ها", "assembly"));
            nav.Controls.Add(CreateNavButton("🔍   بازرسی و اصلاح فایل", "inspector"));
            nav.Controls.Add(CreateNavButton("◈   Surface Design", "surface"));
            nav.Controls.Add(CreateNavButton("▤   Drawing", "drawing"));
            nav.Controls.Add(CreateNavButton("⚙   تنظیمات و آزمون", "settings"));

            var footer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 9), BackColor = _navColor };
            _connectionButton = new Button
            {
                Text = "بررسی اتصال CATIA",
                Dock = DockStyle.Top,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(43, 63, 87),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _connectionButton.FlatAppearance.BorderColor = Color.FromArgb(67, 91, 119);
            _connectionButton.Click += CheckCatiaConnection;

            var footerLabel = new Label
            {
                Text = "Engineering Core · COM V5",
                Dock = DockStyle.Bottom,
                Height = 22,
                ForeColor = Color.FromArgb(160, 179, 201),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8)
            };
            footer.Controls.Add(_connectionButton);
            footer.Controls.Add(footerLabel);

            layout.Controls.Add(brand, 0, 0);
            layout.Controls.Add(nav, 0, 1);
            layout.Controls.Add(footer, 0, 2);
            sidebar.Controls.Add(layout);
            return sidebar;
        }

        private Button CreateNavButton(string text, string pageKey)
        {
            var button = new Button
            {
                Text = text,
                Width = 204,
                Height = 42,
                Margin = new Padding(0, 2, 0, 2),
                FlatStyle = FlatStyle.Flat,
                BackColor = pageKey == "home" ? Color.FromArgb(48, 75, 107) : _navColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f),
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(8, 0, 12, 0),
                Cursor = Cursors.Hand,
                Tag = pageKey
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(41, 60, 83);
            button.Click += (_, _) => ShowPage(pageKey);
            return button;
        }

        private Panel BuildMainPanel()
        {
            var main = new Panel { Dock = DockStyle.Fill, BackColor = _backgroundColor };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                BackColor = _backgroundColor
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));

            var header = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(24, 10, 24, 8)
            };
            _pageTitle = new Label
            {
                Text = "داشبورد",
                Dock = DockStyle.Top,
                Height = 37,
                Font = new Font("Segoe UI", 17, FontStyle.Bold),
                ForeColor = _textColor,
                TextAlign = ContentAlignment.MiddleRight
            };
            _pageSubtitle = new Label
            {
                Text = "تحلیل مهندسی، طراحی پارامتریک، مونتاژ و اتوماسیون واقعی در CATIA V5",
                Dock = DockStyle.Top,
                Height = 26,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(105, 119, 138),
                TextAlign = ContentAlignment.MiddleRight
            };
            _connectionStatus = new Label
            {
                Text = "اتصال بررسی نشده",
                Dock = DockStyle.Left,
                Width = 185,
                ForeColor = Color.FromArgb(90, 105, 125),
                BackColor = Color.FromArgb(238, 242, 247),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            header.Controls.Add(_connectionStatus);
            header.Controls.Add(_pageSubtitle);
            header.Controls.Add(_pageTitle);

            _workspace = new FlowLayoutPanel
            {
                Name = "workspacePanel",
                Dock = DockStyle.Fill,
                BackColor = _backgroundColor,
                AutoScroll = true,
                AutoSize = false,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                RightToLeft = RightToLeft.No,
                Padding = new Padding(22, 18, 22, 18),
                Margin = Padding.Empty
            };
            _workspace.Resize += (_, _) => ResizePageCards();

            var logPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(18, 8, 18, 10)
            };
            var logTitle = new Label
            {
                Text = "گزارش زنده عملیات مهندسی و وضعیت اجرای CAD",
                Dock = DockStyle.Top,
                Height = 25,
                ForeColor = _textColor,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight
            };
            _logBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(249, 250, 252),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9.2f),
                RightToLeft = RightToLeft.Yes,
                DetectUrls = false
            };
            logPanel.Controls.Add(_logBox);
            logPanel.Controls.Add(logTitle);

            layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(_workspace, 0, 1);
            layout.Controls.Add(logPanel, 0, 2);
            main.Controls.Add(layout);
            return main;
        }

        private void ShowPage(string pageKey)
        {
            if (_workspace == null || _workspace.IsDisposed) return;

            _currentPage = pageKey;
            _workspace.SuspendLayout();
            try
            {
                _workspace.Controls.Clear();
                _pageCards.Clear();

                switch (pageKey)
                {
                    case "home":
                        SetPageHeader("داشبورد مهندسی", "دسترسی سریع به دستیار هوشمند، طراحی اجزای ماشین، مونتاژ و بازرسی فایل");
                        BuildHomePage();
                        break;
                    case "ai":
                        SetPageHeader("AI Assistant و موتور فهم مهندسی", "تبدیل درخواست زبان طبیعی به مشخصات مهندسی، محاسبات تحلیلی و برنامه اجرای CAD");
                        BuildAiPage();
                        break;
                    case "sketch":
                        SetPageHeader("Sketch و ترسیم دوبعدی", "ساخت پروفیل‌های بسته و پارامتریک در CATIA با بررسی صحت درخت طراحی");
                        BuildSketchPage();
                        break;
                    case "part":
                        SetPageHeader("Part Design و اجزای ماشین", "ساخت شفت پله‌ای، چرخ‌دنده استاندارد، کوپلینگ فلنجی، پیستون و شاسی");
                        BuildPartDesignPage();
                        break;
                    case "assembly":
                        SetPageHeader("Assembly و مکانیزم‌ها", "ساخت خودکار مجموعه‌های مونتاژی CATProduct (گیربکس، مکانیزم میل‌لنگ-پیستون، کوادکوپتر)");
                        BuildAssemblyPage();
                        break;
                    case "inspector":
                        SetPageHeader("CAD Document Inspector", "بازرسی درخت طراحی، شمارش ویژگی‌ها، بررسی سلامت Update و مدیریت اسناد باز");
                        BuildInspectorPage();
                        break;
                    case "surface":
                        SetPageHeader("Surface Design", "ابزارهای طراحی سطح و هندسه‌های پیچیده");
                        BuildSurfacePage();
                        break;
                    case "drawing":
                        SetPageHeader("Drawing", "نقشه‌های مهندسی، نماها و اندازه‌گذاری");
                        BuildDrawingPage();
                        break;
                    case "settings":
                        SetPageHeader("تنظیمات و آزمون‌های پذیرش", "مدیریت اتصال COM، پوشه کاری و اجرای آزمون‌های خودکار");
                        BuildSettingsPage();
                        break;
                }
            }
            finally
            {
                _workspace.ResumeLayout(true);
                ResizePageCards();
            }

            HighlightCurrentNavigation();
        }

        private void SetPageHeader(string title, string subtitle)
        {
            _pageTitle.Text = title;
            _pageSubtitle.Text = subtitle;
        }

        private void HighlightCurrentNavigation()
        {
            foreach (Control control in Controls)
                HighlightRecursive(control);
        }

        private void HighlightRecursive(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (child is Button button && button.Tag is string key)
                {
                    button.BackColor = key == _currentPage ? Color.FromArgb(48, 75, 107) : _navColor;
                }
                if (child.HasChildren) HighlightRecursive(child);
            }
        }

        private void BuildHomePage()
        {
            var hero = CreateCard("دستیار هوشمند مهندسی مکانیک و اتوماسیون CATIA V5",
                "برخلاف ابزارهای ساده، این سیستم ابتدا هدف مهندسی را تحلیل می‌کند، پارامترهای معلوم و فرض‌شده را تفکیک می‌نماید، محاسبات مهندسی را انجام می‌دهد و سپس هندسه و اسمبلی واقعی را در CATIA می‌سازد.");
            hero.Height = 155;
            AddCardButton(hero, "ورود به دستیار هوشمند (AI)", () => ShowPage("ai"));
            AddCardButton(hero, "طراحی اجزای ماشین", () => ShowPage("part"));
            AddCardButton(hero, "مونتاژ مکانیزم‌ها", () => ShowPage("assembly"));
            AddCardButton(hero, "بازرسی فایل موجود", () => ShowPage("inspector"));
            AddPageCard(hero);

            var quickTemplates = CreateCard("اجرای سریع سناریوهای مهندسی (آزمون‌های پذیرش A تا E)",
                "با کلیک روی هر سناریو، درخواست مهندسی آن در دستیار هوشمند بارگذاری، تحلیل، محاسبه و آماده ساخت در CATIA می‌شود:");
            quickTemplates.Height = 155;
            AddCardButton(quickTemplates, "مجموعه دو چرخ‌دنده", () => OpenAiWithSample("یک مجموعه دو چرخ‌دنده با ماژول 3 و تعداد دندانه 20 و نسبت انتقال 2 و عرض 25 بساز."));
            AddCardButton(quickTemplates, "شفت پله‌ای", () => OpenAiWithSample("یک شفت پله‌ای با ابعاد 20 در 35 و پله میانی 28 در 60 و پله آخر 20 در 35 میلی‌متر طراحی کن."));
            AddCardButton(quickTemplates, "میل‌لنگ و پیستون", () => OpenAiWithSample("یک مکانیزم میل‌لنگ و پیستون با شعاع لنگ 30 و طول شاتون 110 و قطر پیستون 65 میلی‌متر بساز."));
            AddCardButton(quickTemplates, "کوادکوپتر پروازی", () => OpenAiWithSample("یک کوادکوپتر با فاصله قطری 450 میلی‌متر و ملخ 10 اینچ و جرم 1.4 کیلوگرم طراحی و مونتاژ کن."));
            AddPageCard(quickTemplates);

            var statusCard = CreateCard("مدیریت نشست فعلی CATIA",
                "می‌توانی اتصال به CATIA را بررسی کنی، سند فعال را بازرسی نمایی یا اسناد تستی بازمانده در CATIA را ببندی.");
            statusCard.Height = 140;
            AddCardButton(statusCard, "بررسی اتصال CATIA", () => CheckCatiaConnection(this, EventArgs.Empty));
            AddCardButton(statusCard, "بازکردن فایل CAD", OpenCadFile);
            AddCardButton(statusCard, "بستن اسناد بدون تغییر در CATIA", CloseExtraOpenDocuments);
            AddCardButton(statusCard, "اجرای آزمون‌های مرحله ۱", () => RunSelfTests(true));
            AddPageCard(statusCard);
        }

        private void OpenAiWithSample(string samplePrompt)
        {
            ShowPage("ai");
            if (_commandBox != null)
            {
                _commandBox.Text = samplePrompt;
                AnalyzePromptOnly();
            }
        }

        private void BuildAiPage()
        {
            var promptCard = CreateCard("۱. ثبت درخواست مهندسی به زبان طبیعی (فارسی یا انگلیسی)",
                "درخواست را بنویسید. این نسخه از قواعد مهندسی محلی استفاده می‌کند؛ اتصال به مدل زبانی خارجی هنوز پیکربندی نشده است. ابتدا تحلیل را ببینید و پیش از ساخت، فرضیات و محدودیت‌ها را بررسی کنید.");
            promptCard.Height = 235;

            _commandBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                RightToLeft = RightToLeft.Yes,
                TextAlign = HorizontalAlignment.Right,
                Font = new Font("Segoe UI", 10.5f),
                Text = _currentSpecification?.RawUserPrompt ?? "یک مجموعه دو چرخ‌دنده با ماژول 3 و تعداد دندانه 20 و نسبت انتقال 2 و عرض 25 بساز.",
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(16, 78),
                Height = 56,
                Width = promptCard.ClientSize.Width - 36,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            promptCard.Controls.Add(_commandBox);

            AddCardButton(promptCard, "گام ۱: تحلیل مهندسی و محاسبات", AnalyzePromptOnly);
            AddCardButton(promptCard, "گام ۲: ساخت و مونتاژ در CATIA", ExecuteAnalyzedSpecificationInCatia);
            AddCardButton(promptCard, "نمونه کوادکوپتر", () => { _commandBox.Text = "یک کوادکوپتر با قطر 450 میلی‌متر و ملخ 10 اینچ و وزن 1.5 کیلوگرم طراحی کن."; AnalyzePromptOnly(); });
            AddCardButton(promptCard, "نمونه پیستون", () => { _commandBox.Text = "مکانیزم میل‌لنگ و پیستون با شعاع لنگ 28 و شاتون 105 و قطر پیستون 60 میلی‌متر بساز."; AnalyzePromptOnly(); });
            AddPageCard(promptCard);

            var reportCard = CreateCard("۲. خروجی موتور فهم مهندسی، فرضیات، محاسبات تحلیلی و برنامه اجرای CAD",
                "تمامی پارامترها، فرمول‌های محاسباتی و گام‌های اجرای CAD در زیر نمایش داده می‌شوند:");
            reportCard.Height = 360;

            _analysisReportBox = new RichTextBox
            {
                Location = new Point(16, 75),
                Width = reportCard.ClientSize.Width - 36,
                Height = 265,
                ReadOnly = true,
                BackColor = Color.FromArgb(248, 250, 253),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Tahoma", 9.5f),
                RightToLeft = RightToLeft.Yes,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            reportCard.Controls.Add(_analysisReportBox);
            AddPageCard(reportCard);

            if (_currentSpecification == null)
            {
                AnalyzePromptOnly();
            }
            else
            {
                _analysisReportBox.Text = EngineeringIntentAndCalculationEngine.FormatSpecificationReport(_currentSpecification);
            }
        }

        private void AnalyzePromptOnly()
        {
            if (_commandBox == null) return;

            _currentSpecification = _intentEngine.AnalyzeUserRequest(_commandBox.Text);
            string formatted = EngineeringIntentAndCalculationEngine.FormatSpecificationReport(_currentSpecification);

            if (_analysisReportBox != null && !_analysisReportBox.IsDisposed)
            {
                _analysisReportBox.Text = formatted;
            }

            _logger.Info($"تحلیل مهندسی انجام شد: «{_currentSpecification.ProjectTitle}» ({_currentSpecification.ExecutionPlan.Count} گام اجرایی CAD).", "IntentEngine");
        }

        private void ExecuteAnalyzedSpecificationInCatia()
        {
            if (_commandBox != null && (_currentSpecification == null || _currentSpecification.RawUserPrompt != _commandBox.Text))
            {
                AnalyzePromptOnly();
            }

            if (_currentSpecification == null || !_currentSpecification.CanExecuteNow)
            {
                MessageBox.Show(this, "ابتدا یک درخواست معتبر وارد و تحلیل کنید.", "آماده نبودن طرح", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_currentSpecification.HasAssumedParameters)
            {
                string assumptions = string.Join(Environment.NewLine,
                    _currentSpecification.Parameters.Values
                        .Where(p => p.Status == ParameterStatus.AssumedByEngineeringRule)
                        .Select(p => $"• {p.DisplayName}: {p.Value} {p.Unit}"));
                var confirm = MessageBox.Show(this,
                    "بعضی پارامترها از متن شما استخراج نشده‌اند و به‌صورت فرض مهندسی انتخاب شده‌اند:\r\n\r\n" + assumptions + "\r\n\r\nآیا با این مقادیر موقت موافقید و می‌خواهید ساخت در CATIA ادامه یابد؟",
                    "تأیید پارامترهای فرضی", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (confirm != DialogResult.Yes) return;
            }

            var result = _mechanicalEngine.ExecuteDesignSpecification(_currentSpecification, _logger.PartsOutputDirectory);
            if (_analysisReportBox != null && !_analysisReportBox.IsDisposed)
            {
                _analysisReportBox.Text = EngineeringIntentAndCalculationEngine.FormatSpecificationReport(_currentSpecification);
            }

            if (result.FinalState == OperationState.Succeeded)
            {
                MessageBox.Show(
                    this,
                    $"{result.SummaryMessage}\r\n\r\nمسیر ذخیره نهایی:\r\n{result.OutputFilePath}",
                    "اجرای موفق برنامه CAD",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(
                    this,
                    result.SummaryMessage,
                    "خطا در اجرای عملیات CAD",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void BuildSketchPage()
        {
            var basic = CreateCard("ساخت پروفیل بسته و حجم پایه در CATIA",
                "پروفیل‌های بسته با نقاط متصل در صفحه XY ساخته می‌شوند. ارتفاع Pad صفر فقط Sketch را ذخیره می‌کند.");
            basic.Height = 160;
            AddCardButton(basic, "مستطیل / مربع", () => StartShapeWorkflow("مستطیل"));
            AddCardButton(basic, "دایره / استوانه", () => StartShapeWorkflow("دایره"));
            AddCardButton(basic, "مثلث منشوری", () => StartShapeWorkflow("مثلث"));
            AddCardButton(basic, "چندضلعی منتظم", () => StartShapeWorkflow("چندضلعی"));
            AddPageCard(basic);
        }

        private void BuildPartDesignPage()
        {
            var machineElements = CreateCard("ماژول‌های تخصصی طراحی اجزای ماشین (پارامتریک و واقعی در CATIA)",
                "این ابزارها قطعات صنعتی را با ویژگی‌های واقعی در CATIA V5 تولید می‌کنند:");
            machineElements.Height = 160;
            AddCardButton(machineElements, "چرخ‌دنده ساده استاندارد", () => OpenAiWithSample("یک چرخ‌دنده ساده با ماژول 3 و تعداد دندانه 24 و عرض 25 و قطر شفت 20 میلی‌متر بساز."));
            AddCardButton(machineElements, "شفت پله‌ای سه‌مقطعی", () => OpenAiWithSample("یک شفت پله‌ای با ابعاد 20 در 35 و پله میانی 28 در 60 و پله آخر 20 در 35 میلی‌متر بساز."));
            AddCardButton(machineElements, "کوپلینگ فلنجی", () => OpenAiWithSample("یک کوپلینگ فلنجی برای شفت به قطر 22 میلی‌متر طراحی کن."));
            AddCardButton(machineElements, "شاسی کوادکوپتر", () => OpenAiWithSample("یک کوادکوپتر با قطر 450 میلی‌متر طراحی کن."));
            AddPageCard(machineElements);

            var mechanismParts = CreateCard("قطعات مکانیزم میل‌لنگ و پیستون",
                "امکان ساخت مستقل یا یکپارچه میل‌لنگ، شاتون و پیستون با ابعاد سفارشی:");
            mechanismParts.Height = 150;
            AddCardButton(mechanismParts, "میل‌لنگ، شاتون و پیستون", () => OpenAiWithSample("مکانیزم میل‌لنگ و پیستون با شعاع لنگ 30 و شاتون 110 و قطر پیستون 65 میلی‌متر بساز."));
            AddCardButton(mechanismParts, "بلوک مستطیلی سفارشی", () => StartShapeWorkflow("مستطیل"));
            AddCardButton(mechanismParts, "استوانه / بوش", () => StartShapeWorkflow("دایره"));
            AddPageCard(mechanismParts);
        }

        private void BuildAssemblyPage()
        {
            var asmSystems = CreateCard("موتور مونتاژ خودکار سامانه‌های چندقطعه‌ای (CATProduct)",
                "این موتور ابتدا قطعات مستقل را می‌سازد و ذخیره می‌کند، سپس سند CATProduct را ایجاد کرده و قطعات را مونتاژ می‌نماید:");
            asmSystems.Height = 165;
            AddCardButton(asmSystems, "اسمبلی جفت چرخ‌دنده درگیر", () => OpenAiWithSample("یک مجموعه دو چرخ‌دنده با ماژول 3 و دندانه 18 و نسبت انتقال 2 بساز."));
            AddCardButton(asmSystems, "اسمبلی میل‌لنگ، شاتون و پیستون", () => OpenAiWithSample("مکانیزم میل‌لنگ و پیستون با شعاع لنگ 30 و طول شاتون 110 و قطر پیستون 65 میلی‌متر بساز."));
            AddCardButton(asmSystems, "اسمبلی کامل کوادکوپتر (۵ قطعه)", () => OpenAiWithSample("یک کوادکوپتر با فاصله قطری 450 میلی‌متر و ملخ 10 اینچ و وزن 1.4 کیلوگرم بساز."));
            AddCardButton(asmSystems, "اسمبلی ماشین چهارچرخ", () => OpenAiWithSample("یک ماشین اسباب‌بازی با طول 160 و عرض 90 و قطر چرخ 45 میلی‌متر بساز."));
            AddPageCard(asmSystems);
        }

        private void BuildInspectorPage()
        {
            var inspectCard = CreateCard("بازرس فنی اسناد CATIA (CAD Document Inspector)",
                "بررسی ساختار درخت طراحی، تعداد Sketchها و ویژگی‌های حجمی، شمارش زیرمجموعه‌های اسمبلی و تست سلامت Update هندسی:");
            inspectCard.Height = 155;
            AddCardButton(inspectCard, "بازرسی سند فعال در CATIA", () =>
            {
                string rep = _mechanicalEngine.InspectActiveOrSelectedDocument();
                MessageBox.Show(this, rep, "گزارش بازرسی سند فعال", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            AddCardButton(inspectCard, "انتخاب فایل و بازرسی...", () =>
            {
                using var dlg = new OpenFileDialog
                {
                    Title = "انتخاب فایل CATPart یا CATProduct جهت بازرسی فنی",
                    InitialDirectory = _logger.PartsOutputDirectory,
                    Filter = "فایل‌های CATIA|*.CATPart;*.CATProduct"
                };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    string rep = _mechanicalEngine.InspectActiveOrSelectedDocument(dlg.FileName);
                    MessageBox.Show(this, rep, "گزارش بازرسی فایل", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            });
            AddCardButton(inspectCard, "بستن اسناد باز اضافی در CATIA", CloseExtraOpenDocuments);
            AddPageCard(inspectCard);
        }

        private void BuildSurfacePage()
        {
            var card = CreateCard("Surface Design", "ابزارهای طراحی سطح در مراحل بعدی اضافه می‌شوند.");
            card.Height = 140;
            AddCardButton(card, "Extrude / Revolve", () => _logger.Skipped("Surface", "غیرفعال در این نسخه."));
            AddPageCard(card);
        }

        private void BuildDrawingPage()
        {
            var card = CreateCard("Drawing (نقشه‌کشی مهندسی)", "ایجاد نماهای استاندارد از قطعات ساخته‌شده.");
            card.Height = 140;
            AddCardButton(card, "بازکردن فایل نقشه یا قطعه", OpenCadFile);
            AddPageCard(card);
        }

        private void BuildSettingsPage()
        {
            var card = CreateCard("تنظیمات ارتباط COM و پوشه کاری",
                $"ProgID: {CatiaConnectionService.DefaultProgId}\r\nپوشه ذخیره قطعات: {_logger.PartsOutputDirectory}");
            card.Height = 160;
            AddCardButton(card, "بررسی اتصال CATIA", () => CheckCatiaConnection(this, EventArgs.Empty));
            AddCardButton(card, "بازکردن پوشه قطعات", OpenWorkspaceFolder);
            AddCardButton(card, "بازکردن فایل گزارش نشست", OpenCurrentLogFile);
            AddPageCard(card);

            var testCard = CreateCard("اجرای آزمون‌های خودکار سیستم", "اجرای آزمون‌های واحد یا یکپارچه‌سازی:");
            testCard.Height = 150;
            AddCardButton(testCard, "آزمون واحد (بدون CATIA)", () => RunSelfTests(false));
            AddCardButton(testCard, "آزمون کامل با CATIA", () => RunSelfTests(true));
            AddPageCard(testCard);
        }

        private void CloseExtraOpenDocuments()
        {
            try
            {
                int count = _documentManager.GetOpenDocumentCount();
                if (count == 0)
                {
                    MessageBox.Show(this, "هیچ سندی در CATIA باز نیست.", "مدیریت اسناد", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var answer = MessageBox.Show(this,
                    $"در نشست CATIA تعداد {count} سند باز است. این عملیات تلاش می‌کند همهٔ آن‌ها را ببندد، نه فقط اسناد ساخته‌شده توسط این برنامه. اگر سند ذخیره‌نشده‌ای وجود داشته باشد، ممکن است CATIA درخواست ذخیره نشان دهد.\r\n\r\nآیا ادامه می‌دهید؟",
                    "تأیید بستن همهٔ اسناد", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (answer != DialogResult.Yes) return;

                var failures = _documentManager.CloseAllDocuments().Failures;

                if (failures.Count > 0)
                    MessageBox.Show(this, string.Join(Environment.NewLine, failures), "اسناد بسته‌نشده", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                CheckCatiaConnection(this, EventArgs.Empty);
            }
            catch (Exception ex) { _logger.Failed("CloseAllDocuments", ex.Message); MessageBox.Show(this, ex.Message, "خطا در مدیریت اسناد", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void RunSelfTests(bool includeCatia)
        {
            var results = _selfTestRunner.RunPhase1Suite(includeCatia);
            MessageBox.Show(this, Phase1SelfTestRunner.FormatReport(results), "نتیجه آزمون‌ها", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OpenWorkspaceFolder()
        {
            try
            {
                _logger.EnsureWorkspaceDirectories();
                Process.Start(new ProcessStartInfo { FileName = _logger.PartsOutputDirectory, UseShellExecute = true });
            }
            catch (Exception ex) { _logger.Failed("OpenWorkspace", ex.Message); }
        }

        private void OpenCurrentLogFile()
        {
            try
            {
                if (File.Exists(_logger.SessionLogFilePath))
                    Process.Start(new ProcessStartInfo { FileName = _logger.SessionLogFilePath, UseShellExecute = true });
            }
            catch (Exception ex) { _logger.Failed("OpenLogFile", ex.Message); }
        }

        private Panel CreateCard(string title, string description)
        {
            int availableWidth = _workspace.ClientSize.Width > 0 ? _workspace.ClientSize.Width - _workspace.Padding.Horizontal - 12 : 820;
            var card = new Panel { Width = Math.Max(320, availableWidth), Height = 145, BackColor = Color.White, Margin = new Padding(0, 0, 0, 14), Padding = new Padding(16), BorderStyle = BorderStyle.FixedSingle };

            card.Controls.Add(new Label { Text = title, Location = new Point(16, 10), Height = 26, Width = card.ClientSize.Width - 36, Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = _textColor, TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right });
            card.Controls.Add(new Label { Text = description, Location = new Point(16, 38), Height = 44, Width = card.ClientSize.Width - 36, Font = new Font("Segoe UI", 9f), ForeColor = Color.FromArgb(90, 104, 122), TextAlign = ContentAlignment.TopRight, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right });
            return card;
        }

        private void AddPageCard(Panel card)
        {
            int width = _workspace.ClientSize.Width - _workspace.Padding.Horizontal - 12;
            if (width < 320) width = 820;
            card.Width = width;
            _pageCards.Add(card);
            _workspace.Controls.Add(card);
        }

        private void ResizePageCards()
        {
            if (_workspace == null || _workspace.IsDisposed) return;
            int width = _workspace.ClientSize.Width - _workspace.Padding.Horizontal - 12;
            if (width < 320) return;
            foreach (Panel card in _pageCards) if (!card.IsDisposed) card.Width = width;
        }

        private void AddCardButton(Panel card, string text, Action action)
        {
            FlowLayoutPanel? flow = null;
            foreach (Control c in card.Controls) if (c is FlowLayoutPanel f) { flow = f; break; }

            if (flow == null)
            {
                flow = new FlowLayoutPanel { Location = new Point(16, card.Height - 46), Height = 38, Width = card.ClientSize.Width - 32, FlowDirection = FlowDirection.RightToLeft, RightToLeft = RightToLeft.Yes, WrapContents = false, AutoScroll = true, Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, BackColor = Color.White };
                card.Controls.Add(flow); flow.BringToFront();
            }

            var button = new Button { Text = text, Width = Math.Max(110, text.Length * 8 + 34), Height = 32, Margin = new Padding(0, 1, 8, 1), BackColor = text.Contains("گام ۲") ? _successColor : (text.Contains("گام ۱") ? _accentColor : Color.FromArgb(239, 244, 250)), ForeColor = (text.Contains("گام ۱") || text.Contains("گام ۲")) ? Color.White : _textColor, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Cursor = Cursors.Hand };
            button.FlatAppearance.BorderColor = Color.FromArgb(210, 220, 232);
            button.Click += (_, _) => action();
            flow.Controls.Add(button);
        }

        private void CheckCatiaConnection(object? sender, EventArgs e)
        {
            var info = _connectionService.CheckConnection();
            if (info.IsConnected)
            {
                _connectionStatus.Text = $"● متصل ({info.OpenDocumentsCount} سند باز)";
                _connectionStatus.ForeColor = Color.FromArgb(22, 101, 52);
                _connectionStatus.BackColor = Color.FromArgb(220, 252, 231);
            }
            else
            {
                _connectionStatus.Text = "● اتصال برقرار نیست";
                _connectionStatus.ForeColor = Color.FromArgb(153, 27, 27);
                _connectionStatus.BackColor = Color.FromArgb(254, 226, 226);
            }
        }

        private void OpenCadFile()
        {
            using var dialog = new OpenFileDialog { Title = "انتخاب فایل برای بازکردن در CATIA", InitialDirectory = Directory.Exists(_lastSaveDirectory) ? _lastSaveDirectory : _logger.PartsOutputDirectory, Filter = "فایل‌های CAD|*.CATPart;*.CATProduct;*.CATDrawing;*.stp;*.step;*.igs|همه فایل‌ها|*.*" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            _lastSaveDirectory = Path.GetDirectoryName(dialog.FileName) ?? _lastSaveDirectory;
            var res = _documentManager.OpenOrActivateDocument(dialog.FileName);
            if (res.FinalState != OperationState.Succeeded) MessageBox.Show(this, res.SummaryMessage, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void StartShapeWorkflow(string shapeType)
        {
            using var parameters = new ShapeParametersDialog(shapeType);
            if (parameters.ShowDialog(this) != DialogResult.OK) return;

            _logger.EnsureWorkspaceDirectories();
            string cleanType = TextNormalizer.SanitizeIdentifier(shapeType, "Shape");

            using var saveDialog = new SaveFileDialog { Title = "انتخاب نام و محل ذخیره CATPart", InitialDirectory = Directory.Exists(_lastSaveDirectory) ? _lastSaveDirectory : _logger.PartsOutputDirectory, FileName = $"CATIA_{cleanType}_{DateTime.Now:yyyyMMdd_HHmmss}.CATPart", Filter = "CATIA Part (*.CATPart)|*.CATPart", DefaultExt = "CATPart" };
            if (saveDialog.ShowDialog(this) != DialogResult.OK) return;

            string dir = Path.GetDirectoryName(saveDialog.FileName) ?? _logger.PartsOutputDirectory;
            string safeFile = TextNormalizer.SanitizeIdentifier(Path.GetFileNameWithoutExtension(saveDialog.FileName), cleanType) + ".CATPart";
            string finalPath = Path.Combine(dir, safeFile);
            _lastSaveDirectory = dir;

            var result = _partService.CreateShapePart(shapeType, parameters.FirstDimension, parameters.SecondDimension, parameters.PolygonSides, parameters.PadHeight, finalPath);
            if (result.FinalState != OperationState.Succeeded) MessageBox.Show(this, result.SummaryMessage, "خطای مدل‌سازی", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}