using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using EnvDTE;
using EnvDTE80;
using PipelineBuilder;

namespace PipelineBuilderExtension.UI.Forms
{
	public partial class PipelineConfiguration : Form
    {
        #region Variables
        private DTE2 _Root;
		private readonly Dictionary<bool, Action> _AddInViewState = new Dictionary<bool, Action>();
		private readonly Dictionary<Control, string> _InitialValues = new Dictionary<Control, string>();
		private bool _IsInitialized;
        #endregion

        #region Constructor & destructor
        /// <summary>
        /// Initializes a new instance of the <see cref="PipelineConfiguration"/> class.
        /// </summary>
        public PipelineConfiguration()
		{
			InitializeComponent();
			prepareStatemanagement();
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets the project destination.
        /// </summary>
        /// <value>The project destination.</value>
        public string ProjectDestination
        {
            get { return txtProjectLocation.Text; }
        }

        /// <summary>
        /// Gets the build destination.
        /// </summary>
        /// <value>The build destination.</value>
        public string BuildDestination
        {
            get { return txtBinaryOutput.Text; }
        }
        #endregion

        #region Methods
        private void prepareStatemanagement()
		{
			var addInViewOrSingleView = new Control[] { lblAddInView, cbAddInView};
			addInViewOrSingleView.Each(c => _InitialValues.Add(c, 
				(c is ComboBox) ? ((ComboBox)c).SelectedText : c.Text));

			_AddInViewState.Add(true, // if single view project
			                    () => {
			                    	new Control[] {lblHostView, cbHostView}.Each(control => control.Visible = false);
			                    	lblAddInView.Text = "Add-In and Host View (as seen from both) Project";
			                    });
			_AddInViewState.Add(false,
								() => {
									new Control[] { lblHostView, cbHostView }.Each(control => control.Visible = true);
									addInViewOrSingleView.Each(i =>
										{
											if (i is ComboBox)
												((ComboBox)i).SelectedText = _InitialValues[i];
											else i.Text = _InitialValues[i];
										});
								});

			singleViewCheckbox_Changed(cbSingleViewProject, null);
			cbSingleViewProject.CheckedChanged += singleViewCheckbox_Changed;
		}

		private void singleViewCheckbox_Changed(object sender, EventArgs e)
		{
			_AddInViewState[((CheckBox) sender).Checked]();
		}

		#region Get Projects

		public Project SourceProject
		{
			get { return getNullOrProject(cbContractProject); }
		}

		public Project AddInSideAdapterProject
		{
			get { return getNullOrProject(cbAddInSideAdapter); }
		}

		public Project AddInViewProject
		{
			get { return getNullOrProject(cbAddInView); }
		}

		public Project HostSideAdapterProject
		{
			get { return getNullOrProject(cbHostSideAdapter); }
		}

		public Project HostViewProject
		{
			get { return getNullOrProject(cbHostView); }
		}

		private static Project getNullOrProject(ComboBox view)
		{
			return view.SelectedItem == null ? null : ((PlaceHolder<Project>)view.SelectedItem).Project;
		}

		#endregion

		#region Output selections

		private void btnProjectOutput_Click(object sender, EventArgs e)
		{
            // Get current path
		    string currentPath = Path.GetFullPath(txtProjectLocation.Text);

            // Let user select a path
		    string selectedPath = PathHelper.SelectDirectory("Select project directory", currentPath);

            // Check if we have a result
            if (!string.IsNullOrEmpty(selectedPath)) txtProjectLocation.Text = selectedPath;
		}

		private void btnOutput_Click(object sender, EventArgs e)
		{
            // Get current path
            string directoryName = Path.GetDirectoryName(SourceProject.FileName);
            string currentPath = Path.GetFullPath(directoryName.Combine(txtBinaryOutput.Text));

            // Let user select a path
            string selectedPath = PathHelper.SelectDirectory("Select build output directory", currentPath);

            // Check if we have a result
            if (!string.IsNullOrEmpty(selectedPath)) txtBinaryOutput.Text = selectedPath;
		}

		#endregion

		internal void Initialize(DTE2 root)
		{
			if (_IsInitialized) return;
			_IsInitialized = true;
			_Root = root;

			// TODO: Use the code inspection possibilities
			var combos = new List<ComboBox>
			             	{
			             		cbContractProject,
			             		cbAddInSideAdapter,
			             		cbHostSideAdapter,
			             		cbAddInView,
			             		cbHostView
			             	};

			var names = new List<List<string>>
			            	{
                                new List<string>{"Contracts", "Contract", "Plugins", "Plugin"},
								new List<string>{"AddInSideAdapter", "AddInSideFacade", "AddInSide", "Adapter"}, 
								new List<string>{"HostSideAdapter", "HostSideFacade", "HostSide", "Adapter"},
								new List<string>{"AddInView", "AddIn", "View"},
								new List<string>{"HostView", "Host", "View"}
			            	};

			combos.Each(c => c.Items.Clear());

			int count = 0;
			foreach (var project in PipelineBuilderCommand.GetProjectsFromSolution(root))
			{
				// project full name may be "" sometimes, which causes the folder parsing to fail,
				// because then the project is not a "real" project.
				// What type of ProjectKinds this project is, I have no idea.
				if (project.Kind == ProjectKinds.vsProjectKindSolutionFolder
					|| project.FullName == string.Empty) continue;

				count++;
				var tuple = new PlaceHolder<Project>(project.Name, project);
				foreach (var box in combos) box.Items.Add(tuple);
			}

			// No projects in solution, or only "folder"-projects: 
			if (count == 0) return;

			var contractProject = selectBest(combos, names);
			if (contractProject == null) return;

			cbContractProject.SelectedItem = contractProject;
			initializeProject(contractProject);

            // Try to load information from settings
            if (!string.IsNullOrEmpty(Properties.Settings.Default.ContractSourceProject)) cbContractProject.Text = Properties.Settings.Default.ContractSourceProject;
            if (!string.IsNullOrEmpty(Properties.Settings.Default.ProjectLocation)) txtProjectLocation.Text = Properties.Settings.Default.ProjectLocation;
            if (!string.IsNullOrEmpty(Properties.Settings.Default.BuildLocation)) txtBinaryOutput.Text = Properties.Settings.Default.BuildLocation;
		}

		private static Project selectBest(IList<ComboBox> combos, IList<List<string>> names)
		{
			Project firstProject = null;

			var selected = new List<string>();

			for (int iBox = 0; iBox < combos.Count; iBox++)
			{
				var combo = combos[iBox];
				if (iBox == 0 && combos[0].Items.Count > 0)
				{
					firstProject = ((PlaceHolder<Project>) combos[0].Items[0]).Project;
				}

				bool found = false;
				foreach (var name in names[iBox])
				{
					if (found) break;
					for (int i = 0; i < combo.Items.Count; i++)
					{
						var projectName = ((PlaceHolder<Project>) combo.Items[i]).Name;
						if (!projectName.Contains(name) || selected.Contains(projectName)) continue;
						selected.Add(projectName);
						combo.SelectedIndex = i;
						found = true;
						break;
					}

					if (!found)
					{
						// don't have a value for non-existant items
						combo.SelectedIndex = combo.Items.Add(new PlaceHolder<Project>("", null));
					}
				}

			}

			return firstProject;
		}

		private void cbContractProject_SelectedValueChanged(object sender, EventArgs e)
		{
			initializeProject(SourceProject);
		}

		private void initializeProject(Project p)
		{
			if (p == null) return;

			txtProjectLocation.Text = Path.GetFullPath(Path.GetDirectoryName(p.FullName).Combine(".."));
			txtBinaryOutput.Text = Path.GetDirectoryName(Path.GetFullPath(p.GetOutputAssembly()));
		}

		private void btnGuessProjects_Click(object sender, EventArgs e)
		{
			_IsInitialized = false;
			Initialize(_Root);
        }

        private void pathHelpButton_Click(object sender, EventArgs e)
        {
            PathHelpForm pathHelpForm = new PathHelpForm();
            pathHelpForm.ShowDialog();
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            // Should we store settings for the next time?
            if (rememberSettingsCheckBox.Checked)
            {
                // Get settings
                Properties.Settings settings = Properties.Settings.Default;

                // Store values
                settings.ContractSourceProject = cbContractProject.Text;
                settings.ProjectLocation = txtProjectLocation.Text;
                settings.BuildLocation = txtBinaryOutput.Text;

                // Save
                settings.Save();
            }
        }
        #endregion
    }
}