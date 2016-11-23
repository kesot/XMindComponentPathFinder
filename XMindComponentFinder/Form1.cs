using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

namespace XMindComponentFinder
{
    public partial class Form1 : Form
    {
        private string scProjPath;
        XDocument document;
        public Form1()
        {
            InitializeComponent();
            openFileDialog1.Filter = "Xmind book|*.xmind";
            openFileDialog1.FileOk += (sender, args) =>
            {
                scProjPath = openFileDialog1.FileName;
                using (ZipArchive archive = ZipFile.Open(scProjPath, ZipArchiveMode.Read))
                {
                    ZipArchiveEntry entry = archive.GetEntry("content.xml");
                    using (var reader = XmlReader.Create(entry.Open()))
                    {
                        document = XDocument.Load(reader);
                    }
                }
            };
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openFileDialog1.ShowDialog();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (document == null)
            {
                MessageBox.Show("Load document firstly");
                return;
            }

            var componentName = textBox1.Text.Trim();

            if (componentName.Length == 0)
            {
                MessageBox.Show("Specify component for search");
                return;
            }

            var nameSpace = document.Root.Name.NamespaceName;
            var foundElements = document.Descendants(XName.Get("topic", nameSpace))
                .Where(t => componentName.Equals(t.Element(XName.Get("title", nameSpace))?.Value, StringComparison.InvariantCultureIgnoreCase));

            // remove first part (sheet name)
            var paths = foundElements.Select(elem => Regex.Replace(GetPath(elem), "^\\/[^/]*\\/", ""));

            paths = DemergeLayouts(paths).OrderBy(s => s);

            richTextBox2.Text = string.Join(Environment.NewLine, paths);
        }

        static IEnumerable<string> DemergeLayouts(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                var parts = path.Split('/');
                var multiLayoutIndex = parts.Select((p, i) => new { Index = i, Part = p })
                    .SingleOrDefault(p => p.Part.Contains("&"))?.Index;
                if (multiLayoutIndex == null)
                    yield return path;
                else
                {
                    var layouts = Regex.Split(parts[multiLayoutIndex.Value], " *& *");
                    foreach (var layout in layouts)
                    {
                        parts[multiLayoutIndex.Value] = layout;
                        yield return string.Join("/", parts);
                    }
                }
            }
        }

        private static Dictionary<string, string> namingNullMap = new Dictionary<string, string>
        {
            {"directly within ", null},
            {" components", null }
        };

        private static string GetPath(XElement element)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }

            Func<XElement, string> relativeXPath = e =>
            {
                var title = e.Element(XName.Get("title", e.Name.NamespaceName))?.Value;


                if (title == null)
                    return null;

                var foundKey = namingNullMap.Keys.FirstOrDefault(k => title.Contains(k));
                if (foundKey != null)
                {
                    title = namingNullMap[foundKey];
                }

                return title?.Replace(Environment.NewLine, "").Insert(0, "/");
            };

            var ancestors = element.Ancestors().Select(relativeXPath);

            return string.Concat(ancestors.Reverse().ToArray()) +
                   relativeXPath(element);
        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                button1_Click(null, null);
                // Enter key pressed
            }
        }
    }
}
