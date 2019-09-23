﻿using Nucleus.Gaming;
using Nucleus.Gaming.Coop;
using Nucleus.Gaming.Generic.Step;
using Nucleus.Gaming.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Nucleus.Gaming.Windows.Interop;
using WindowScrape.Constants;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nucleus.Coop
{
    /// <summary>
    /// Central UI class to the Nucleus Coop application
    /// </summary>
    public partial class MainForm : BaseForm
    {
        private Form settingsForm = null;

        private int currentStepIndex;
        private bool formClosing;
        private ContentManager content;
        private IGameHandler handler;

        private GameManager gameManager;
        private Dictionary<UserGameInfo, GameControl> controls;

        private SearchDisksForm form;

        private GameControl currentControl;
        private UserGameInfo currentGameInfo;
        private GenericGameInfo currentGame;
        private GameProfile currentProfile;
        private bool noGamesPresent;
        private List<UserInputControl> stepsList;
        private UserInputControl currentStep;

        private PositionsControl positionsControl;
        private PlayerOptionsControl optionsControl;
        private JSUserInputControl jsControl;

        private int KillProcess_HotkeyID = 1;
        private int TopMost_HotkeyID = 2;
        private int StopSession_HotkeyID = 3;

        public string version = "v0.9.6.1 ALPHA";
        public Control SelectedControl { get; protected set; }
        //public event Action<object, Control> SelectedChanged;

        private Thread handlerThread;

        private bool TopMostToggle = true;

        private enum ShowWindowEnum
        {
            Hide = 0,
            ShowNormal = 1, ShowMinimized = 2, ShowMaximized = 3,
            Maximize = 3, ShowNormalNoActivate = 4, Show = 5,
            Minimize = 6, ShowMinNoActivate = 7, ShowNoActivate = 8,
            Restore = 9, ShowDefault = 10, ForceMinimized = 11
        };

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool ShowWindow(IntPtr hWnd, ShowWindowEnum flags);

        public MainForm()
        {
            InitializeComponent();

            sideInfoLbl.Text = "Modded by ZeroFox" + "\n" + version;

            positionsControl = new PositionsControl();
            Form settingsForm = new Form(this, positionsControl);

            positionsControl.Paint += PositionsControl_Paint;

            settingsForm.RegHotkeys(this);

            controls = new Dictionary<UserGameInfo, GameControl>();
            gameManager = new GameManager();

            optionsControl = new PlayerOptionsControl();
            jsControl = new JSUserInputControl();

            positionsControl.OnCanPlayUpdated += StepCanPlay;
            optionsControl.OnCanPlayUpdated += StepCanPlay;
            jsControl.OnCanPlayUpdated += StepCanPlay;

            // selects the list of games, so the buttons look equal
            list_Games.Select();
            SelectedControl = null;

            //list_Games.AutoScroll = false;
            //int vertScrollWidth = SystemInformation.VerticalScrollBarWidth;
            //list_Games.Padding = new Padding(0, 0, vertScrollWidth, 0);
        }

        private void PositionsControl_Paint(object sender, PaintEventArgs e)
        {
            if (positionsControl.isDisconnected)
            {
                DPIManager.ForceUpdate();
                positionsControl.isDisconnected = false;
            }
        }

        protected override Size DefaultSize
        {
            get
            {
                return new Size(1070, 740);
            }
        }

        //protected override void OnGotFocus(EventArgs e)
        //{
        //    base.OnGotFocus(e);
        //    //this.TopMost = true;
        //    this.BringToFront();

        //    System.Diagnostics.Debug.WriteLine("Got Focus");
        //}
        //private void CheckForManualExit()
        //{
        //    while (true)
        //    {
        //        //you need to use Invoke because the new thread can't access the UI elements directly
        //        MethodInvoker mi = delegate () { if (handler == null) { GoToStep(0); this.Controls.Clear(); this.InitializeComponent(); RefreshGames(); t.Abort(); } };
        //        this.Invoke(mi);
        //        Thread.Sleep(500);
        //    }
        //}

        //protected override void OnDeactivate(EventArgs e)
        //{
        //    if (t != null && t.IsAlive)
        //        t.Abort();
        //}

        //protected override void OnActivated(EventArgs e)
        //{
            
        //    if (btn_Play.Text == "S T O P")
        //    {
        //        if (handler == null)
        //        {
        //            ////MessageBox.Show("handle not null and has ended");
        //            ////SetBtnToPlay();
        //            //if (handlerThread != null)
        //            //{
        //            //    handlerThread.Abort();
        //            //    handlerThread = null;
        //            //}
        //            ////list_Games_SelectedChanged(null, null);
        //            //RefreshGames();
        //            //Invoke(new Action(SetBtnToPlay));
        //            //btn_Play.Enabled = false;
        //            GoToStep(0);
        //            this.Controls.Clear();
        //            this.InitializeComponent();
        //            RefreshGames();
        //        }
        //        else
        //        {
        //            //btn_Play.Enabled = false;
        //            t = new System.Threading.Thread(CheckForManualExit);
        //            t.Start();
        //        }
        //    }
        //}

        protected override void WndProc(ref Message m)
        {
            //int msg = m.Msg;
            //LogManager.Log(msg.ToString());
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == KillProcess_HotkeyID)
            {
                //System.Diagnostics.Process.GetCurrentProcess().Kill();
                Close();
            }
            else if (m.Msg == 0x0312 && m.WParam.ToInt32() == TopMost_HotkeyID)
            {
                if (TopMostToggle && handler != null)
                {
                    try
                    {
                        Process[] procs = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(currentGame.ExecutableName.ToLower()));
                        if (procs.Length > 0)
                        {
                            for (int i = 0; i < procs.Length; i++)
                            {
                                IntPtr hWnd = procs[i].MainWindowHandle;
                                User32Interop.SetWindowPos(hWnd, new IntPtr(-2), 0, 0, 0, 0, (uint)(PositioningFlags.SWP_NOSIZE | PositioningFlags.SWP_NOMOVE));
                                ShowWindow(hWnd, ShowWindowEnum.Minimize);
                            }
                        }
                    }
                    catch { }
                    User32Util.ShowTaskBar();
                    //currentGame.LockMouse = false;
                    this.Activate();
                    this.BringToFront();
                    TopMostToggle = false;
                }
                else if(!TopMostToggle && handler != null)
                {
                    Process[] procs = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(currentGame.ExecutableName.ToLower()));
                    if (procs.Length > 0)
                    {
                        for (int i = 0; i < procs.Length; i++)
                        {
                            IntPtr hWnd = procs[i].MainWindowHandle;
                            ShowWindow(hWnd, ShowWindowEnum.Restore);
                            User32Interop.SetWindowPos(hWnd, new IntPtr(-1), 0, 0, 0, 0, (uint)(PositioningFlags.SWP_NOSIZE | PositioningFlags.SWP_NOMOVE));
                        }
                    }
                    User32Util.HideTaskbar();
                    //currentGame.LockMouse = true;
                    //this.Activate();
                    //this.BringToFront();
                    TopMostToggle = true;
                }
            }
            else if (m.Msg == 0x0312 && m.WParam.ToInt32() == StopSession_HotkeyID)
            {
                if (btn_Play.Text == "S T O P")
                {
                    WindowState = FormWindowState.Normal;
                    this.BringToFront();
                    btn_Play.PerformClick();
                }

            }
            base.WndProc(ref m);
        }

        public void RefreshGames()
        {
            lock (controls)
            {
                foreach (var con in controls)
                {
                    if (con.Value != null)
                    {
                        con.Value.Dispose();
                    }
                }
                this.list_Games.Controls.Clear();
                controls.Clear();

                List<UserGameInfo> games = gameManager.User.Games;
                for (int i = 0; i < games.Count; i++)
                {
                    UserGameInfo game = games[i];
                    NewUserGame(game);
                }

                if (games.Count == 0)
                {
                    noGamesPresent = true;
                    GameControl con = new GameControl(null, null);
                    con.Width = list_Games.Width;
                    con.Text = "No games";
                    this.list_Games.Controls.Add(con);
                }
            }

            DPIManager.ForceUpdate();
            GameManager.Instance.SaveUserProfile();
        }

        public void NewUserGame(UserGameInfo game)
        {
            if (game.Game == null || !game.IsGamePresent())
            {
                return;
            }

            if (noGamesPresent)
            {
                noGamesPresent = false;
                RefreshGames();
                return;
            }

            GameControl con = new GameControl(game.Game, game);
            con.Width = list_Games.Width;

            controls.Add(game, con);
            this.list_Games.Controls.Add(con);

            ThreadPool.QueueUserWorkItem(GetIcon, game);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RefreshGames();

            DPIManager.ForceUpdate();
        }

        private void GetIcon(object state)
        {
            UserGameInfo game = (UserGameInfo)state;
            Icon icon = Shell32.GetIcon(game.ExePath, false);

            Bitmap bmp = icon.ToBitmap();
            icon.Dispose();
            game.Icon = bmp;

            lock (controls)
            {
                if (controls.ContainsKey(game))
                {
                    GameControl control = controls[game];
                    control.Invoke((Action)delegate ()
                    {
                        control.Image = game.Icon;
                    });
                }
            }
        }

        private void list_Games_SelectedChanged(object arg1, Control arg2)
        {
            currentControl = (GameControl)arg1;
            currentGameInfo = currentControl.UserGameInfo;
            if (currentGameInfo == null)
            {
                btn_delete.Visible = false;
                btn_details.Visible = false;
                return;
            }

            StepPanel.Visible = true;

            currentGame = currentGameInfo.Game;

            btn_Play.Enabled = false;

            stepsList = new List<UserInputControl>();
            stepsList.Add(positionsControl);
            stepsList.Add(optionsControl);
            for (int i = 0; i < currentGame.CustomSteps.Count; i++)
            {
                stepsList.Add(jsControl);
            }

            currentProfile = new GameProfile();
            currentProfile.InitializeDefault(currentGame);

            gameNameControl.GameInfo = currentGameInfo;

            btn_delete.Location = new Point(384 + (gameNameControl.Width - 100), 39);
            btn_delete.Visible = true;
            btn_details.Location = new Point(384 + (gameNameControl.Width - 100), 8);
            btn_details.Visible = true;

            if (content != null)
            {
                content.Dispose();
            }

            // contnet manager is shared withing the same game
            content = new ContentManager(currentGame);
            GoToStep(0);
        }

        private void EnablePlay()
        {
            btn_Play.Enabled = true;
        }

        private void StepCanPlay(UserControl obj, bool canProceed, bool autoProceed)
        {
            if (!canProceed)
            {
                btn_Next.Enabled = false;
                return;
            }

            if (currentStepIndex + 1 > stepsList.Count - 1)
            {
                EnablePlay();
                return;
            }

            if (autoProceed)
            {
                GoToStep(currentStepIndex + 1);
            }
            else
            {
                btn_Next.Enabled = true;
            }
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            GoToStep(currentStepIndex + 1);
        }

        private void KillCurrentStep()
        {
            currentStep?.Ended();
            this.StepPanel.Controls.Clear();
        }

        private void GoToStep(int step)
        {
            btnBack.Enabled = step > 0;
            if (step >= stepsList.Count)
            {
                return;
            }

            if (step >= 2)
            {
                // Custom steps
                List<CustomStep> customSteps = currentGame.CustomSteps;
                int customStepIndex = step - 2;
                CustomStep customStep = customSteps[0];

                if (customStep.UpdateRequired != null)
                {
                    customStep.UpdateRequired();
                }

                if (customStep.Required)
                {
                    jsControl.CustomStep = customStep;
                    jsControl.Content = content;
                }
                else
                {
                    EnablePlay();
                    return;
                }
            }

            KillCurrentStep();

            currentStepIndex = step;
            currentStep = stepsList[step];
            currentStep.Size = StepPanel.Size;
            currentStep.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;

            currentStep.Initialize(currentGameInfo, currentProfile);

            btn_Next.Enabled = currentStep.CanProceed && step != stepsList.Count - 1;

            StepPanel.Controls.Add(currentStep);
            currentStep.Size = StepPanel.Size; // for some reason this line must exist or the PositionsControl get messed up

            label_StepTitle.Text = currentStep.Title;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);

            formClosing = true;
            //if (handler.FakeFocus != null)
            //{
            //    handler.FakeFocus.Abort();
            //}
            if (handler != null)
            {
                handler.End();
            }

        }

        private void btn_Play_Click(object sender, EventArgs e)
        {
            if (btn_Play.Text == "S T O P")
            {
                if (handler.FakeFocus != null)
                {
                    handler.FakeFocus.Abort();
                }
                if (handler != null)
                {
                    handler.End();
                }
                SetBtnToPlay();
                btn_Play.Enabled = false;
                this.Controls.Clear();
                this.InitializeComponent();
                RefreshGames();

                return;
            }

            btn_Play.Text = "S T O P";
            btnBack.Enabled = false;

            handler = gameManager.MakeHandler(currentGame);
            handler.Initialize(currentGameInfo, GameProfile.CleanClone(currentProfile));
            handler.Ended += handler_Ended;

            gameManager.Play(handler);
            if (handler.TimerInterval > 0)
            {
                handlerThread = new Thread(UpdateGameManager);
                handlerThread.Start();
            }

            if (currentGame.HideTaskbar)
            {
                User32Util.HideTaskbar();
            }

            if (currentGame.HideDesktop)
            {
                foreach(Screen screen in Screen.AllScreens)
                {
                    System.Windows.Forms.Form hform = new System.Windows.Forms.Form();
                    hform.BackColor = Color.Black;
                    hform.Location = new Point(0, 0);
                    hform.Size = screen.WorkingArea.Size;
                    this.Size = screen.WorkingArea.Size;
                    hform.FormBorderStyle = FormBorderStyle.None;
                    hform.StartPosition = FormStartPosition.Manual;
                    //hform.TopMost = true;
                    hform.Show();
                }
            }

            WindowState = FormWindowState.Minimized;
        }

        private void SetBtnToPlay()
        {
            //btn_Play.Visible = true;
            btn_Play.Text = "P L A Y";
        }

        private void handler_Ended()
        {
            handler = null;
            if (handlerThread != null)
            {
                handlerThread.Abort();
                handlerThread = null;
            }
            Invoke(new Action(SetBtnToPlay));
        }

        private void UpdateGameManager(object state)
        {
            for (;;)
            {
                try
                {
                    if (gameManager == null || formClosing || handler == null)
                    {
                        break;
                    }

                    string error = gameManager.Error;
                    if (!string.IsNullOrEmpty(error))
                    {
                        MessageBox.Show(error, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        handler_Ended();
                        return;
                    }

                    handler.Update(handler.TimerInterval);
                    Thread.Sleep(TimeSpan.FromMilliseconds(handler.TimerInterval));
                }
                catch(ThreadAbortException)
                {
                    return;
                }
                catch { }
            }
        }

        private void arrow_Back_Click(object sender, EventArgs e)
        {
            currentStepIndex--;
            if (currentStepIndex < 0)
            {
                currentStepIndex = 0;
                return;
            }
            GoToStep(currentStepIndex);
        }

        private void arrow_Next_Click(object sender, EventArgs e)
        {
            currentStepIndex = Math.Min(currentStepIndex++, stepsList.Count - 1);
            GoToStep(currentStepIndex);
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog open = new OpenFileDialog())
            {
                open.Filter = "Game Executable Files|*.exe";
                if (open.ShowDialog() == DialogResult.OK)
                {
                    string path = open.FileName;

                    List<GenericGameInfo> info = gameManager.GetGames(path);

                    if (info.Count > 1)
                    {
                        GameList list = new GameList(info);
                        DPIManager.ForceUpdate();

                        if (list.ShowDialog() == DialogResult.OK)
                        {
                            UserGameInfo game = gameManager.TryAddGame(path, list.Selected);

                            if (game == null)
                            {
                                MessageBox.Show("Game already in your library!");
                            }
                            else
                            {
                                MessageBox.Show("Game accepted as " + game.Game.GameName);
                                RefreshGames();
                            }
                        }
                    }
                    else if (info.Count == 1)
                    {
                        UserGameInfo game = gameManager.TryAddGame(path, info[0]);
                        MessageBox.Show("Game accepted as " + game.Game.GameName);
                        RefreshGames();
                    }
                    else
                    {
                        MessageBox.Show("Unknown game");
                    }
                }
            }
        }

        private void btnAutoSearch_Click(object sender, EventArgs e)
        {
            if (form != null)
            {
                return;
            }

            form = new SearchDisksForm(this);
            //DPIManager.AddForm(form);

            form.FormClosed += Form_FormClosed;
            form.Show();
            SetUpForm(form);

            DPIManager.ForceUpdate();
        }

        private void Form_FormClosed(object sender, FormClosedEventArgs e)
        {
            form = null;
        }

        private void btnShowTaskbar_Click(object sender, EventArgs e)
        {
            User32Util.ShowTaskBar();
        }

        private void SettingsBtn_Click(object sender, EventArgs e)
        {
            settingsForm = new Form(this, positionsControl);
            settingsForm.Show();
        }

        private void Btn_delete_Click(object sender, EventArgs e)
        {
            DeleteGame();
        }

        private void Btn_details_Click(object sender, EventArgs e)
        {
            GetDetails();
        }

        private void DetailsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GetDetails();
        }

        private void DeleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DeleteGame();
        }

        private void GetDetails()
        {
            string userProfile = gameManager.GetUserProfilePath();

            if (File.Exists(userProfile))
            {
                string jsonString = File.ReadAllText(userProfile);
                JObject jObject = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonString) as JObject;

                JArray games = jObject["Games"] as JArray;
                for (int i = 0; i < games.Count; i++)
                {
                    string gameGuid = jObject["Games"][i]["GameGuid"].ToString();
                    string profiles = jObject["Games"][i]["Profiles"].ToString();
                    string exePath = jObject["Games"][i]["ExePath"].ToString();

                    if (gameGuid == currentGameInfo.GameGuid && exePath == currentGameInfo.ExePath)
                    {
                        MessageBox.Show(string.Format("Game Name: {0}\nGame Guid: {1}\nProfiles: {2}\nExe Path: {3}\nScript Filename: {4}", currentGameInfo.Game.GameName, gameGuid, profiles, exePath, currentGameInfo.Game.JsFileName), "Game Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }

        private void DeleteGame()
        {
            string userProfile = gameManager.GetUserProfilePath();

            if (File.Exists(userProfile))
            {
                string jsonString = File.ReadAllText(userProfile);
                JObject jObject = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonString) as JObject;

                JArray games = jObject["Games"] as JArray;
                for (int i = 0; i < games.Count; i++)
                {
                    string gameGuid = jObject["Games"][i]["GameGuid"].ToString();
                    string profiles = jObject["Games"][i]["Profiles"].ToString();
                    string exePath = jObject["Games"][i]["ExePath"].ToString();

                    if (gameGuid == currentGameInfo.GameGuid && exePath == currentGameInfo.ExePath)
                    {
                        DialogResult dialogResult = MessageBox.Show("Are you sure you want to delete " + currentGameInfo.Game.GameName + "?", "Confirm deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                        if (dialogResult == DialogResult.Yes)
                        {
                            gameManager.User.Games.RemoveAt(i);
                            jObject["Games"][i].Remove();
                            string output = JsonConvert.SerializeObject(jObject, Formatting.Indented);
                            File.WriteAllText(userProfile, output);
                            this.Controls.Clear();
                            this.InitializeComponent();
                            RefreshGames();
                        }
                    }
                }
            }
        }
    }
}
