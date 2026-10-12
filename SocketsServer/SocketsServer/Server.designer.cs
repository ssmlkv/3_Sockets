namespace Sockets
{
    partial class frmMain
    {
        /// <summary>
        /// Требуется переменная конструктора.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Освободить все используемые ресурсы.
        /// </summary>
        /// <param name="disposing">истинно, если управляемый ресурс должен быть удален; иначе ложно.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Код, автоматически созданный конструктором форм Windows

        /// <summary>
        /// Обязательный метод для поддержки конструктора - не изменяйте
        /// содержимое данного метода при помощи редактора кода.
        /// </summary>
        private void InitializeComponent()
        {
            this.rtbMessages = new System.Windows.Forms.RichTextBox();
            this.gbClients = new System.Windows.Forms.GroupBox();
            this.lbClients = new System.Windows.Forms.ListBox();
            this.gbClients.SuspendLayout();
            this.SuspendLayout();
            // 
            // rtbMessages
            // 
            this.rtbMessages.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtbMessages.Location = new System.Drawing.Point(0, 0);
            this.rtbMessages.Name = "rtbMessages";
            this.rtbMessages.ReadOnly = true;
            this.rtbMessages.Size = new System.Drawing.Size(389, 205);
            this.rtbMessages.TabIndex = 0;
            this.rtbMessages.Text = "";
            // 
            // gbClients
            //
            this.gbClients.Dock = System.Windows.Forms.DockStyle.Right;
            this.gbClients.Size = new System.Drawing.Size(150, 250);
            this.gbClients.Name = "gbClients";
            this.gbClients.Text = "Участники (0)";
            this.gbClients.Controls.Add(this.lbClients);
            this.gbClients.TabIndex = 1;
            //
            // lbClients
            //
            this.lbClients.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lbClients.Name = "lbClients";
            this.lbClients.IntegralHeight = false;
            this.lbClients.HorizontalScrollbar = true;
            this.lbClients.TabIndex = 0;
            //
            // frmMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(549, 250);
            this.MinimumSize = new System.Drawing.Size(480, 220);
            this.Controls.Add(this.rtbMessages);
            this.Controls.Add(this.gbClients);
            this.Name = "frmMain";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Сервер";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.frmMain_FormClosing);
            this.gbClients.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox gbClients;
        private System.Windows.Forms.ListBox lbClients;
        private System.Windows.Forms.RichTextBox rtbMessages;
    }
}

