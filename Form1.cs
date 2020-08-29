using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Xbim.Ifc;
using Xbim.Ifc4.Kernel;
using Xbim.Ifc4.MeasureResource;
using Xbim.Ifc4.PropertyResource;
using Xbim.Ifc4.Interfaces;
using IIfcProject = Xbim.Ifc4.Interfaces.IIfcProject;
using Xbim.Ifc2x3.ProductExtension;
using Xbim.Common.Geometry;
using Xbim.Common.XbimExtensions;
using Xbim.ModelGeometry.Scene;

namespace GammaPlugin0._1
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            outputBox.AppendText("Plugin launched successfully");
        }



        /// BUTTON1: Distribute point sources between elements in the BIM
        private void button1_Click(object sender, EventArgs e)
        {
            // Setup the editor
            var editor = new XbimEditorCredentials
            {
                ApplicationDevelopersName = "Oskar",
                ApplicationFullName = "xxx",
                ApplicationIdentifier = "99991200",
                ApplicationVersion = "1.12",
                EditorsFamilyName = "Billington",
                EditorsGivenName = "Oskar",
                EditorsOrganisationName = "xxx"
            };

            // Choose an IFC file to work with        
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.ShowDialog();
            string filename = dialog.FileName;

            string newLine = Environment.NewLine;
            
            // Check if the file is valid and continue
            if (!filename.ToLower().EndsWith(".ifc"))
            {
                // Output error if the file is the wrong format
                outputBox.AppendText(newLine + newLine + "Error: select an .ifc-file");
            }
            else
            {
                // Open the selected file (## Not sure what the response is to a corrupt/invalid .ifc-file)
                using (var model = IfcStore.Open(filename, editor, 1.0))
                {
                    // Output success when the file has been opened
                    string reversedName = Form1.ReversedString(filename);
                    int filenameShortLength = reversedName.IndexOf("\\");
                    string filenameShort = filename.Substring(filename.Length - filenameShortLength, filenameShortLength);
                    outputBox.AppendText(newLine + newLine + filenameShort + " opened successfully for editing");

                    // Get all the objects in the model and create a list of the lowest-level elements' names
                    var objs = model.Instances.OfType<IfcObjectDefinition>();
                    var els = model.Instances.OfType<IIfcBuildingElement>();
                    List<string> objNames = new List<string>();
                    outputBox.AppendText(newLine + "Objects count: " + els.Count().ToString());
                    foreach (var el in els)
                    {
                        outputBox.AppendText(newLine + "Object found: " + el.Name.ToString());
                        objNames.Add(el.Name.ToString());
                    }

                    // Create a unique name to later save the modified model as
                    string modifiedFilename = filenameShort.Substring(0, filenameShort.Length - 4) + "_Modified.IFC";
                    int k = 0;
                    while (File.Exists(modifiedFilename))
                    {
                        k += 1;
                        modifiedFilename = filenameShort.Substring(0, filenameShort.Length - 4) + "_Modified(" + k.ToString() + ").IFC";
                    }

                    // Create a folder with a unique name to store the source point text files. The first folder index is the same as the modified IFC-model index, the second index is in case it already exists
                    string newFoldername;
                    if (k == 0)
                    {
                        newFoldername = filenameShort.Substring(0, filenameShort.Length - 4) + "_GammaSources_Distribution";
                    }
                    else
                    {
                        newFoldername = filenameShort.Substring(0, filenameShort.Length - 4) + "_GammaSources_Distribution(" + k.ToString() + ")";
                    }
                    int i = 0;
                    while (Directory.Exists(newFoldername))
                    {
                        i += 1;
                        newFoldername = filenameShort.Substring(0, filenameShort.Length - 4) + "_GammaSources_Distribution(" + k.ToString() + ")(" + i.ToString() + ")";
                    }
                    Directory.CreateDirectory(newFoldername); // (!) Gets stored in the project folder > bin > Debug
                    outputBox.AppendText(newLine + "New folder has been created: " + newFoldername);
                    string outputDirectory = Directory.GetCurrentDirectory(); // Store root output directory and temporarily enter the new subdirectory:
                    Directory.SetCurrentDirectory(newFoldername);
                    

                    /// SETUP A POINT SOURCE PROPERTY AND REFERECED TEXT FILE FOR EACH OBJECT
                    using (var txn = model.BeginTransaction("Store Point Source(s)"))
                    {
                        // Iterate over all the lowest-level elements to initiate new properties
                        foreach (var obj in objs)
                        {
                            if (objNames.Contains(obj.Name.ToString()))
                            {
                                // Create new property set to host properties
                                var pSetRel = model.Instances.New<IfcRelDefinesByProperties>(r =>
                                {
                                    r.GlobalId = Guid.NewGuid();
                                    r.RelatingPropertyDefinition = model.Instances.New<IfcPropertySet>(pSet =>
                                    {
                                        pSet.Name = "Point Sources";
                                        pSet.HasProperties.Add(model.Instances.New<IfcPropertySingleValue>(p =>
                                        {
                                            // Create the unique filename to store the sources related to this object
                                            string textFilename = obj.Name + "_GammaSources.xyz";
                                            int t = 0;
                                            while (File.Exists(textFilename))
                                            {
                                                t += 1;
                                                textFilename = obj.Name + "_GammaSources(" + t.ToString() + ").xyz";
                                            }

                                            // Create the empty text file
                                            var temp = File.CreateText(textFilename);
                                            temp.Close();
                                            outputBox.AppendText(newLine + "Point Source text file has been created: " + textFilename);

                                            // Store the associated text file name as a property of the object
                                            p.Name = "GammaSources filename"; // This name has one dependency; do not edit it
                                            p.NominalValue = new IfcText(textFilename);

                                        }));
                                    });
                                });
                                
                                // Add property to the object
                                pSetRel.RelatedObjects.Add(obj);

                                // Rename the object
                                outputBox.AppendText(newLine + "Point Source property added to: " + obj.Name);
                                obj.Name += "_withGammaSources";

                            }
                        }

                        // Commit changes to this model
                        txn.Commit();
                    };



                    /// STORE EXAMPLE POINT SOURCES IN THE OBJECT TEXT FILES   ## 
                    // Open the global xyz-file
                    OpenFileDialog dialog2 = new OpenFileDialog();
                    dialog2.ShowDialog();
                    string filenameXYZ = dialog2.FileName;
                 
                    // Check if the file is valid and continue
                    if (!filenameXYZ.ToLower().EndsWith(".xyz"))
                    {
                        // Output error if the file is the wrong format
                        outputBox.AppendText(newLine + "Error: select an .xyz-file");
                    }
                    else
                    {
                        // Output success when the file has been opened
                        string reversedNameXYZ = Form1.ReversedString(filenameXYZ);
                        int filenameShortLengthXYZ = reversedNameXYZ.IndexOf("\\");
                        string filenameShortXYZ = filenameXYZ.Substring(filenameXYZ.Length - filenameShortLengthXYZ, filenameShortLengthXYZ);
                        outputBox.AppendText(newLine + filenameShortXYZ + " opened and ready for distribution");
                        
                        // Open the selected file
                        string[] points = System.IO.File.ReadAllLines(filenameXYZ);
                        foreach (string point in points)
                        {
                            //outputBox.AppendText(newLine + point);

                            // Select which object's property text file to store the point in ## INSERT COLLISION ALGORITHM HERE
                            string correctTextfile = ""; 
                            foreach (var el in els) // AS A TEST: store all the points in all the objects - should here isntead use collision detection to pick ONE element
                            {
                                // Read the property to get the filename to open
                                var properties = el.IsDefinedBy
                                    .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                                    .SelectMany(r => ((IIfcPropertySet)r.RelatingPropertyDefinition).HasProperties)
                                    .OfType<IIfcPropertySingleValue>();
                                foreach (var property in properties)
                                    if (property.Name == "GammaSources filename")
                                    {   
                                        correctTextfile = property.NominalValue.ToString();
                                    }

                                // Add the point to the file
                                using (System.IO.StreamWriter file = new System.IO.StreamWriter(correctTextfile, true))
                                {
                                    file.WriteLine(point);
                                    file.Close();
                                    outputBox.AppendText(newLine + "Point sources have been distributed and stored in " + newFoldername);
                                }
                            }

                        }
                    }

                    
                    // Save the changed model with a new name - does not overwrite existing files 
                    Directory.SetCurrentDirectory(outputDirectory); // Go back up one level to the root output directory
                    model.SaveAs(modifiedFilename); // (!) Gets stored in the project folder > bin > Debug
                    outputBox.AppendText(newLine + modifiedFilename + " has been saved" + newLine);
                       
                };
            }
            
        }



        /// BUTTON2: Reconstruct the global point source file from a modified BIM with associated point source files in a folder
        private void button2_Click(object sender, EventArgs e)
        {
            // Setup the editor
            var editor = new XbimEditorCredentials
            {
                ApplicationDevelopersName = "Oskar",
                ApplicationFullName = "xxx",
                ApplicationIdentifier = "99991200",
                ApplicationVersion = "1.12",
                EditorsFamilyName = "Billington",
                EditorsGivenName = "Oskar",
                EditorsOrganisationName = "xxx"
            };

            // Choose an IFC file to work with        
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.ShowDialog();
            string filename = dialog.FileName;

            string newLine = Environment.NewLine;

            // Check if the file is valid and continue
            if (!filename.ToLower().EndsWith(".ifc"))
            {
                // Output error if the file is the wrong format
                outputBox.AppendText(newLine + newLine + "Error: select an .ifc-file");
            }
            else
            {
                // Open the selected file
                using (var model = IfcStore.Open(filename, editor, 1.0))
                {
                    // Output success when the file has been opened
                    string reversedName = Form1.ReversedString(filename);
                    int filenameShortLength = reversedName.IndexOf("\\");
                    string filenameShort = filename.Substring(filename.Length - filenameShortLength, filenameShortLength);
                    outputBox.AppendText(newLine + newLine + filenameShort + " opened successfully for editing");

                    // Get all the objects in the model and create a list of the lowest-level elements' names
                    var objs = model.Instances.OfType<IfcObjectDefinition>();
                    var els = model.Instances.OfType<IIfcBuildingElement>();
                    List<string> objNames = new List<string>();
                    outputBox.AppendText(newLine + "Objects count: " + els.Count().ToString());
                    foreach (var el in els)
                    {
                        objNames.Add(el.Name.ToString());
                    }



                    // ####### Opening the model is necessary later when I need to derive the coordinate transformation 

                    // Create a unique filename, create the (blank) global point source file
                    string reconstructedXYZ = filenameShort.Substring(0, filenameShort.Length - 4) + "_GlobalActivity.xyz";
                    int i = 0;
                    while (File.Exists(reconstructedXYZ))
                    {
                        i += 1;
                        reconstructedXYZ = filenameShort.Substring(0, filenameShort.Length - 4) + "_GlobalActivity(" + i.ToString() + ").xyz";
                    }
                    var temp = File.CreateText(reconstructedXYZ);
                    temp.Close();
                    outputBox.AppendText(newLine + "Global Activity text file has been created: " + reconstructedXYZ);



                    // Select the folder containing the point source sub-set files
                    List<string> allPoints = new List<string>();
                    using (var fbd = new FolderBrowserDialog())
                    {
                        DialogResult selectedFolder = fbd.ShowDialog();

                        if (selectedFolder == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                        {
                            string[] files = System.IO.Directory.GetFiles(fbd.SelectedPath);

                            // Robustness test
                            outputBox.AppendText(newLine + "Files found: " + files.Length.ToString());
                            if (files.Length != els.Count())
                            {
                                outputBox.AppendText(newLine + "WARNING: The selected folder may not correspond to the selected .ifc-file");
                            }

                            // ### insert reverse coordinate transformation
                            // Open each file and copy/paste the points into the global file 
                            foreach (var file in files)
                            {
                                // (These three lines are only to print out a neater name - may not be worth the computation) -- or is this useful below(?):
                                string revN = Form1.ReversedString(file);
                                int fShortL = revN.IndexOf("\\");
                                string fShort = file.Substring(file.Length - fShortL, fShortL); // Name of file in folder
                                string revN2 = Form1.ReversedString(fShort);
                                int fL = revN2.IndexOf("_");
                                string fShort2 = fShort.Substring(0, fShort.Length - fL - 1); // Name of element referred to by the file


                                /// (CAN NOT just store the transformation in the text file - the whole point is, what if a user customizes the BIM... must be re-derived)
                                // Find the element associated with the file (BuildingElement or ObjectDefinition to extract coordinates?)


                                // Find the global position of the element's origin



                                // ... simply ADD the global position to the point here (in the loop below)?? What about if the object is rotated in the BIM...?... (affine transformation)



                                // Read the correct file and append the points to the global file
                                string[] points = System.IO.File.ReadAllLines(file);
                                foreach (var point in points)
                                {
                                    allPoints.Add(point);
                                }
                                outputBox.AppendText(newLine + "Point sources have been extracted from: " + fShort);
                            }
                        }
                    }


                    using (System.IO.StreamWriter file = new System.IO.StreamWriter(reconstructedXYZ, true))
                    {
                        foreach (var point in allPoints)
                        {
                            file.WriteLine(point);
                        }
                        file.Close();
                        outputBox.AppendText(newLine + "All the point sources have been stored in " + reconstructedXYZ);
                    }









                }
            }
        }






        // Temporary button: Gathering info about coordinate transformation and bounding box/geometry
        private void button3_Click(object sender, EventArgs e)
        {
            // Select model
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.ShowDialog();
            string filename = dialog.FileName;

            string newLine = Environment.NewLine;

            // Check if the file is valid and continue
            if (!filename.ToLower().EndsWith(".ifc"))
            {
                // Output error if the file is the wrong format
                outputBox.AppendText(newLine + newLine + "Error: select an .ifc-file");
            }
            else
            {
                // Open the selected file
                using (var model = IfcStore.Open(filename))
                {
                    Xbim3DModelContext context = new Xbim3DModelContext(model);
                    context.CreateContext();

                    List<XbimShapeGeometry> geometries = context.ShapeGeometries().ToList();
                    List<XbimShapeInstance> instances = context.ShapeInstances().ToList();
                    outputBox.AppendText(newLine + "All geometries: " + geometries.ToString());
                    outputBox.AppendText(newLine + "All instances: " + instances.ToString());

                    //Check all the instances
                    foreach (var instance in instances)
                    {
                        var transfor = instance.Transformation; // Localisation/transformation matrix  ### IS THIS ONLY LOCAL TRANSFORMATION (UP ONE LEVEL) OR GLOBAL POSTITION PRE-ASSEMBLED? OTHERWISE NEED TO STACKKK!!! (Computationally heavy with lots of matrix inverses) -- CHECK BY EDITING A BIMMM
                        outputBox.AppendText(newLine + "Transformation matrix: " + transfor.ToString()); // To transform mathematically: [x' y' z' 1]' = transfor * [x y z 1]'

                        XbimShapeGeometry geometry = context.ShapeGeometry(instance);   // Instance's geometry
                        XbimRect3D box = geometry.BoundingBox; // Bounding box


                        byte[] data = ((IXbimShapeGeometryData)geometry).ShapeData;

                        // (All faces and triangulations)
                        using (var stream = new MemoryStream(data))
                        {
                            using (var reader = new BinaryReader(stream))
                            {
                                var mesh = reader.ReadShapeTriangulation();

                                List<XbimFaceTriangulation> faces = mesh.Faces as List<XbimFaceTriangulation>;
                                List<XbimPoint3D> vertices = mesh.Vertices as List<XbimPoint3D>;
                            }
                        }
                    }
                }
            }
        }










        private void Form1_Load(object sender, EventArgs e)
        {

        }



        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }



        // Reverse string-function
        static string ReversedString(string text)
        {
            if (text == null) return null;

            // this was posted by petebob as well 
            char[] array = text.ToCharArray();
            Array.Reverse(array);
            return new String(array);
        }

       

        // ## Functions: Global to local & local to global coordinate transformations

    }
}