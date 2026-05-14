using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CodeCreater
{
    public partial class ActivationForm : Form
    {
        private TextBox machineCodeInput;
        private ComboBox typeSelector;
        private Button generateButton;
        private Label outputLabel;
        private Button copyButton;

        private static readonly string saltA = "XiaoDianNiMaSiLe";
        private static readonly string saltB = "XTeleportProMax";

        public ActivationForm()
        {
            InitializeComponent();
            // 设置窗口大小
            this.Size = new System.Drawing.Size(540, 215);
            this.StartPosition = FormStartPosition.CenterScreen;
            CreateControls();
        }
        // 在窗口大小改变时重新计算并设置控件的位置以使其居中
        private void ActivationForm_SizeChanged(object sender, EventArgs e)
        {
            CenterControls();
        }
        private void CenterControls()
        {
            int centerX = this.ClientSize.Width / 2;
            int startY = 15;

            // 修改下面这行来改变控件之间的间距
            int verticalSpacing = 35;

            machineCodeInput.Location = new System.Drawing.Point(centerX - machineCodeInput.Width / 2, startY);
            typeSelector.Location = new System.Drawing.Point(centerX - typeSelector.Width / 2, startY + verticalSpacing);
            generateButton.Location = new System.Drawing.Point(centerX - generateButton.Width - 10, startY + 2 * verticalSpacing);
            copyButton.Location = new System.Drawing.Point(centerX + 10, startY + 2 * verticalSpacing);
            outputLabel.Location = new System.Drawing.Point(centerX - outputLabel.Width / 2, startY + 3 * verticalSpacing);
        }
        private void CreateControls()
        {
            machineCodeInput = new TextBox
            {
                Location = new System.Drawing.Point(15, 15),
                Size = new System.Drawing.Size(250, 20)
            };

            typeSelector = new ComboBox
            {
                Location = new System.Drawing.Point(15, 50),
                Size = new System.Drawing.Size(250, 20)
            };
            typeSelector.Items.Add(1);
            typeSelector.Items.Add(2);
            typeSelector.DropDownStyle = ComboBoxStyle.DropDownList;

            generateButton = new Button
            {
                Location = new System.Drawing.Point(15, 85),
                Size = new System.Drawing.Size(100, 23),
                Text = "Generate"
            };
            generateButton.Click += GenerateButton_Click;

            outputLabel = new Label
            {
                Location = new System.Drawing.Point(15, 120),
                Size = new System.Drawing.Size(500, 20)
            };
            copyButton = new Button
            {
                Location = new System.Drawing.Point(125, 85), // 根据你的布局调整位置
                Size = new System.Drawing.Size(100, 23),
                Text = "Copy"
            };
            copyButton.Click += CopyButton_Click; // 这个是新按钮的点击事件

            Controls.Add(machineCodeInput);
            Controls.Add(typeSelector);
            Controls.Add(generateButton);
            Controls.Add(outputLabel);
            Controls.Add(copyButton);
            // 订阅窗口大小改变事件
            this.SizeChanged += ActivationForm_SizeChanged;

            // 居中显示控件
            CenterControls();
        }
        private void CopyButton_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(outputLabel.Text))
            {
                Clipboard.SetText(outputLabel.Text);
            }
        }

        private void GenerateButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(machineCodeInput.Text) || typeSelector.SelectedIndex < 0)
            {
                outputLabel.Text = "Please input machine code and select type";
                return;
            }

            int type = (int)typeSelector.SelectedItem;
            string machineCode = machineCodeInput.Text;

            outputLabel.Text = GenerateActivationCode(machineCode, type);
        }

        internal string GenerateActivationCode(string machineCode, int type)
        {
            string salt = "";
            switch (type)
            {
                case 1:
                    salt = saltA;
                    break;
                case 2:
                    salt = saltB;
                    break;
            }
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // Add salt to the machine code
                string saltedMachineCode = machineCode + salt;

                // Compute and get hash
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(saltedMachineCode));

                // Convert byte array to a string
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
