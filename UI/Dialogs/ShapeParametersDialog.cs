using System;
using System.Drawing;
using System.Windows.Forms;

namespace CatiaAIAssistant.UI.Dialogs
{
    internal sealed class ShapeParametersDialog : Form
    {
        private readonly NumericUpDown _first, _second, _sides, _padHeight;
        private readonly Label _secondLabel, _sidesLabel;

        public double FirstDimension => (double)_first.Value;
        public double SecondDimension => (double)_second.Value;
        public int PolygonSides => (int)_sides.Value;
        public double PadHeight => (double)_padHeight.Value;

        public ShapeParametersDialog(string shapeType)
        {
            Text = $"تنظیمات {shapeType}";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            Size = new Size(480, 365);
            Font = new Font("Segoe UI", 9.5f);
            RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;

            Controls.Add(new Label { Text = $"ابعاد {shapeType} را وارد کن", Location = new Point(22, 15), Size = new Size(410, 28), Font = new Font("Segoe UI", 12, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight });

            string firstL = shapeType == "مستطیل" ? "طول X" : shapeType == "دایره" ? "شعاع" : shapeType == "مثلث" ? "قاعده" : "شعاع محیطی";
            AddLabel(firstL, 52); _first = AddNumber(52, 30);

            _secondLabel = new Label { Text = shapeType == "مثلث" ? "ارتفاع مثلث" : "عرض Y", Location = new Point(215, 91), Size = new Size(215, 24), TextAlign = ContentAlignment.MiddleRight };
            Controls.Add(_secondLabel); _second = AddNumber(88, 20);

            _sidesLabel = new Label { Text = "تعداد اضلاع", Location = new Point(215, 130), Size = new Size(215, 24), TextAlign = ContentAlignment.MiddleRight };
            Controls.Add(_sidesLabel); _sides = AddNumber(127, 6, 3, 100);

            AddLabel("ارتفاع Pad (صفر = فقط Sketch)", 167); _padHeight = AddNumber(167, 10, 0, 10000);

            if (shapeType == "دایره" || shapeType == "چندضلعی") { _second.Visible = false; _secondLabel.Visible = false; }
            if (shapeType != "چندضلعی") { _sides.Visible = false; _sidesLabel.Visible = false; }

            var ok = new Button { Text = "تأیید و ذخیره", DialogResult = DialogResult.OK, Location = new Point(215, 265), Size = new Size(215, 36), BackColor = Color.FromArgb(36, 105, 180), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            var cancel = new Button { Text = "انصراف", DialogResult = DialogResult.Cancel, Location = new Point(105, 265), Size = new Size(95, 36), FlatStyle = FlatStyle.Flat };
            Controls.Add(ok); Controls.Add(cancel); AcceptButton = ok; CancelButton = cancel;
        }

        private void AddLabel(string text, int top) => Controls.Add(new Label { Text = text, Location = new Point(215, top), Size = new Size(215, 24), TextAlign = ContentAlignment.MiddleRight });
        private NumericUpDown AddNumber(int top, decimal val, decimal min = 1, decimal max = 10000)
        {
            var n = new NumericUpDown { Location = new Point(22, top), Size = new Size(175, 25), Minimum = min, Maximum = max, Value = val, DecimalPlaces = 2 };
            Controls.Add(n); return n;
        }
    }
}