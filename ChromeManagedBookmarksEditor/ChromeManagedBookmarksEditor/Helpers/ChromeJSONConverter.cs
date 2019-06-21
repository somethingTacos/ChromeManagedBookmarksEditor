﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChromeManagedBookmarksEditor.Model;
using System.Windows;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace ChromeManagedBookmarksEditor.Helpers
{
    public static class ChromeJSONConverter
    {
        // Async methods to convert ManagedBookmarks object into JSON code
        public static async Task<string> ConvertToJSON(Folder RootFolder)
        {
            string ReturnableJSON = string.Empty;

            await Task.Run(() => 
            {
                ReturnableJSON = _ConvertToJSON(RootFolder);
            });

            return ReturnableJSON;
        }

        private static string _ConvertToJSON(Folder RootFolder)
        {
            string convertedJSON = string.Empty;

            string topLevelName = $"[{{\"toplevel_name\":\"{RootFolder.Name}\"}},";

            convertedJSON = topLevelName;

            ObservableCollection<string> JSONCollection = new ObservableCollection<string>();

            string GetFolderJSONContent(Folder folder)
            {
                ObservableCollection<string> folderContents = new ObservableCollection<string>();
                if (folder.folders.Count > 0)
                {
                    foreach (Folder subfolder in folder.folders)
                    {
                        folderContents.Add($"{{\"name\":\"{subfolder.Name}\",\"children\":[]}}");
                    }
                }

                if (folder.URLs.Count > 0)
                {
                    foreach (URL url in folder.URLs)
                    {
                        folderContents.Add($"{{\"name\":\"{url.Name}\",\"url\":\"{url.Url}\"}}");
                    }
                }

                string joinedContents = String.Join(",", folderContents);
                if(joinedContents == "" && folder.FolderIndex != 0) { joinedContents = "EMPTY"; }
                return joinedContents;
            }

            void IterateSubFolders(Folder folder)
            {
                JSONCollection.Add(GetFolderJSONContent(folder));
                if (folder.folders.Count > 0)
                {
                    foreach (Folder subFolder in folder.folders)
                    {
                        IterateSubFolders(subFolder);
                    }
                }
            }

            //Get RootFolder content
            JSONCollection.Add(GetFolderJSONContent(RootFolder));

            //iterate RootFolders.folders
            if (RootFolder.folders.Count > 0)
            {
                foreach (Folder subfolder in RootFolder.folders)
                {
                    IterateSubFolders(subfolder);
                }
            }

            convertedJSON += JSONCollection[0];

            //replace folder children [] with actual contents
            for(int i = 1; i < JSONCollection.Count(); i++)
            {
                var regex = new Regex(Regex.Escape("[]"));
                convertedJSON = regex.Replace(convertedJSON, $"[{JSONCollection[i]}]", 1);
            }

            convertedJSON = convertedJSON.Replace("[EMPTY]", "[]");
            convertedJSON += "]";

            return convertedJSON;
        }

        // Async Methods to parse JSON code into a ManagedBookmarks object
        public static async Task<ManagedBookmarks> ParseJSON(string JSONCode)
        {
            ManagedBookmarks ReturnableManagedBookmarks = new ManagedBookmarks();

            await Task.Run(() =>
            {
                ReturnableManagedBookmarks = _ParseJSON(JSONCode);
            });

            return ReturnableManagedBookmarks;
        }

        private static ManagedBookmarks _ParseJSON(string JSONCode)
        {
            /* This one, I'm going to have to think about a bit more...
             * 
             * Visual aid:
             * 
             * JSON CODE IN -> 
             * Top Level Name             - [{"toplevel_name":"Root Folder"},
             * Folder 1 with children     - {"name":"FOLDER 1","children":[{"name":"FOLDER 2","children":[]},{"name":"F1 Content","url":"F1.content"}]},
             * Folder 3 with children     - {"name":"FOLDER 3","children":[{"name":"F3 Content","url":"F3.content"}]},
             * Remaining root folder urls - {"name":"Root Folder Content","url":"root.content"}]
             * 
             * STRIPPED CODE ARRAY ->
             * toplevel_name:Root Folder         - toplevel_name give the root folder name
             * name:TEST                         - any name is the start of an object
             * children:[name:Another Folder     - children denotes it being a folder and starts looking for objects to add to that folders name (previous line)
             * children:[name:AF URL             - same as above, Another Folder is now a known folder and these are it's child objects
             * url:sldfksdljkf]                  - ] denotes the end of the current folder's child objects (Another Folder)
             * name:TEST URL                     - 
             * url:slkdfslkjf]                   - url denotes a URL object using the name above (TEST URL), also ending of TEST's child objects
             * name:ROOT URL                     - 
             * url:jalsdjlsdf                    - 
             * 
             * Idea: -> Could try stripping all the unneccessary stuff from the JSON code and then translating it to a ManagedBookmarks object.
             *     : 
             *     : Going to require some testing...
             *     
             * 
             */
            ManagedBookmarks ParsedBookmarks = new ManagedBookmarks();
            try
            {


                string StrippedJSONData = JSONCode.Replace("{", "");
                StrippedJSONData = StrippedJSONData.Replace("}", "");
                StrippedJSONData = StrippedJSONData.Replace("\"", "");
                StrippedJSONData = StrippedJSONData.Remove(0, 1);
                StrippedJSONData = StrippedJSONData.Remove(StrippedJSONData.Count() - 1, 1);

                List<string> JSONDataList = new List<string>();
                JSONDataList = StrippedJSONData.Split(',').ToList<string>();

                Folder WorkingFolder = new Folder();
                string lastName = "";

                foreach (string data in JSONDataList)
                {
                    string[] CurrentData = data.Split(':');

                    switch (CurrentData[0])
                    {
                        case "toplevel_name":
                            {
                                ParsedBookmarks.RootFolder.Name = CurrentData[1];
                                ParsedBookmarks.RootFolder.FolderIndex = 0;
                                WorkingFolder = ParsedBookmarks.RootFolder;
                                break;
                            }
                        case "children":
                            {
                                Folder newFolder = new Folder { Name = lastName };
                                newFolder.Parent = WorkingFolder;
                                newFolder.FolderIndex = WorkingFolder.FolderIndex + 1;
                                WorkingFolder.folders.Add(newFolder);

                                if (data != "children:[]")
                                {
                                    lastName = data.Substring(10).Split(':')[1];
                                    WorkingFolder = WorkingFolder.folders.Where(x => x == newFolder).FirstOrDefault();
                                }
                                break;
                            }
                        case "name":
                            {
                                lastName = CurrentData[1];
                                break;
                            }
                        case "url":
                            {
                                URL newURL = new URL { Name = lastName, Url = String.Join(":", CurrentData) };

                                bool FolderEnd = CurrentData[CurrentData.Count() - 1].EndsWith("]");
                                newURL.Url = newURL.Url.Remove(0, 4);

                                WorkingFolder.URLs.Add(newURL);

                                if (FolderEnd) { WorkingFolder = WorkingFolder.Parent; }

                                break;
                            }
                    }
                }
            }
            catch(Exception ex) { }

            return ParsedBookmarks;
        }
    }
}


//All of this non-sense is going to be reworked, just moving it to the correct class for now.

//private async Task<ObservableCollection<ManagedBookmarks>> LoadJSON(string json)
//{
//    ObservableCollection<ManagedBookmarks> bookmarks = new ObservableCollection<ManagedBookmarks>();

//    await Task.Run(() =>
//    {
//        bookmarks = _LoadJSON(json);
//    });

//    return bookmarks;
//}

////this is going to be completly redone in the TreeConverter class
//private ObservableCollection<ManagedBookmarks> _LoadJSON(string json)
//{
//    try
//    {
//        ObservableCollection<ManagedBookmarks> Bookmarks = new ObservableCollection<ManagedBookmarks>();
//        ObservableCollection<Folder> FoldersList = new ObservableCollection<Folder>();

//        var root = JsonConvert.DeserializeObject<ObservableCollection<RootFolder>>(json);
//        var parsedfolders = JsonConvert.DeserializeObject<ObservableCollection<ParsedFolders>>(json);
//        var urls = JsonConvert.DeserializeObject<ObservableCollection<URL>>(json);


//        ObservableCollection<URL> clearNullURLs(ObservableCollection<URL> Urls)
//        {
//            for (int i = 0; i < Urls.Count; i++)
//            {
//                if (Urls[i].Name == null || Urls[i].Url == null)
//                {
//                    Urls.RemoveAt(i);
//                    i--;
//                }
//            }

//            return Urls;
//        }

//        Folder iterateChildObjects(string subJson)
//        {
//            try
//            {
//                var parsedSubfolders = JsonConvert.DeserializeObject<List<ParsedFolders>>(subJson);
//                Folder subfolder = new Folder();
//                Folder newfolder = new Folder();

//                for (int i = 0; i < parsedSubfolders.Count; i++)
//                {
//                    if (parsedSubfolders[i].children != null)
//                    {
//                        if (parsedSubfolders[i].children.ToString().Contains("children"))
//                        {
//                            newfolder = iterateChildObjects(parsedSubfolders[i].children.ToString());
//                        }

//                        if (newfolder.Name != null)
//                        {
//                            subfolder.folders = new ObservableCollection<Folder>();
//                            subfolder.folders.Add(newfolder);
//                        }

//                        subfolder.Name = parsedSubfolders[i].Name.ToString();
//                        subfolder.URLs = JsonConvert.DeserializeObject<ObservableCollection<URL>>(parsedSubfolders[i].children.ToString());

//                        subfolder.URLs = clearNullURLs(subfolder.URLs);
//                    }
//                }

//                return subfolder;
//            }
//            catch (Exception ex)
//            {
//                MessageBox.Show(ex.Message);
//                return null;
//            }

//        }

//        for (int i = 0; i < parsedfolders.Count; i++)
//        {
//            if (parsedfolders[i].children != null)
//            {
//                Folder folder = new Folder();
//                folder.folders = new ObservableCollection<Folder>();

//                if (parsedfolders[i].children.ToString().Contains("children"))
//                {
//                    Folder subfolder = new Folder();
//                    subfolder = iterateChildObjects(parsedfolders[i].children.ToString());
//                    folder.folders.Add(subfolder);
//                }

//                folder.Name = parsedfolders[i].Name.ToString();
//                folder.URLs = JsonConvert.DeserializeObject<ObservableCollection<URL>>(parsedfolders[i].children.ToString());
//                folder.URLs = clearNullURLs(folder.URLs);
//                FoldersList.Add(folder);
//            }
//        }

//        urls = clearNullURLs(urls);

//        Bookmarks.Add(new ManagedBookmarks()
//        {
//            toplevel_name = root[0].toplevel_name.ToString(),

//            Folders = FoldersList,

//            URLs = urls
//        });

//        return Bookmarks;
//    }
//    catch (Exception ex)
//    {
//        MessageBox.Show(ex.Message);
//        return null;
//    }
//}

//private async Task<string> ConvertTreeToJSON(ObservableCollection<ManagedBookmarks> chromeBookmarks)
//{
//    string chromeJSONcode = String.Empty;

//    await Task.Run(() =>
//    {
//        chromeJSONcode = _ConvertTreeToJSON(chromeBookmarks);
//    });

//    return chromeJSONcode;
//}

////this is going to be completly redone in the TreeConverter class
//private string _ConvertTreeToJSON(ObservableCollection<ManagedBookmarks> chromeBookmarks)
//{
//    string ConvertedJSON = String.Empty;

//    string toplevel_name = string.Format("[{{\"toplevel_name\":\"{0}\"}},", chromeBookmarks[0].toplevel_name.ToString());

//    ConvertedJSON = toplevel_name;

//    string iterateChildObject(int index, List<Folder> Folders, List<URL> Urls, bool isChildOfChild)
//    {
//        string childObjects = String.Empty;

//        for (int o = 0; o < Folders.Count; o++)
//        {
//            if (Folders[o].folders != null || Folders[o].URLs != null)
//            {
//                string Name = Folders[o].Name;

//                if (o is 0)
//                {
//                    childObjects += string.Format("{{\"name\":\"{0}\",\"children\":[", Name);
//                }
//                else
//                {
//                    childObjects += string.Format(",{{\"name\":\"{0}\",\"children\":[", Name);
//                }

//                List<Folder> childFolders = new List<Folder>();

//                if (Folders[o].folders != null)
//                {
//                    foreach (Folder folder in Folders[o].folders)
//                    {
//                        childFolders.Add(folder);
//                    }
//                }

//                List<URL> childURLs = new List<URL>();

//                foreach (URL url in Folders[o].URLs)
//                {
//                    childURLs.Add(url);
//                }

//                string childItems = iterateChildObject(o, childFolders, childURLs, true);

//                childObjects += childItems;
//            }
//        }


//        for (int n = 0; n < Urls.Count; n++)
//        {
//            string urlName = Urls[n].Name.ToString();
//            string url = Urls[n].Url.ToString();

//            if (index is 0 && n == 0 || n == 0)
//            {
//                childObjects += string.Format("{{\"name\":\"{0}\",\"url\":\"{1}\"}}", urlName, url);
//            }
//            else
//            {
//                childObjects += string.Format(",{{\"name\":\"{0}\",\"url\":\"{1}\"}}", urlName, url);
//            }
//        }

//        if (isChildOfChild ? index + 1 < Folders.Count || chromeBookmarks[0].Folders[index].URLs.Count != 0 : index + 1 < chromeBookmarks[0].Folders.Count)
//        {
//            childObjects += "]},";
//        }
//        else
//        {
//            childObjects += "]}";
//        }

//        return childObjects;
//    }

//    for (int i = 0; i < chromeBookmarks[0].Folders.Count; i++)
//    {
//        string Name = chromeBookmarks[0].Folders[i].Name.ToString();

//        if (i is 0 || i + 1 == chromeBookmarks[0].Folders.Count)
//        {
//            ConvertedJSON += string.Format("{{\"name\":\"{0}\",\"children\":[", Name);
//        }
//        else
//        {
//            ConvertedJSON += string.Format(",{{\"name\":\"{0}\",\"children\":[", Name);
//        }

//        List<Folder> childFolders = new List<Folder>();


//        foreach (Folder folder in chromeBookmarks[0].Folders[i].folders)
//        {
//            childFolders.Add(folder);
//        }

//        List<URL> childURLs = new List<URL>();

//        foreach (URL url in chromeBookmarks[0].Folders[i].URLs)
//        {
//            childURLs.Add(url);
//        }

//        string subFolders = iterateChildObject(i, childFolders, childURLs, false);

//        ConvertedJSON += subFolders;
//    }


//    for (int i = 0; i < chromeBookmarks[0].URLs.Count; i++)
//    {
//        string urlName = chromeBookmarks[0].URLs[i].Name.ToString();
//        string url = chromeBookmarks[0].URLs[i].Url.ToString();

//        if (i is 0 ? chromeBookmarks[0].Folders.Count is 0 : false)
//        {
//            ConvertedJSON += string.Format("{{\"name\":\"{0}\",\"url\":\"{1}\"}}", urlName, url);
//        }
//        else
//        {
//            ConvertedJSON += string.Format(",{{\"name\":\"{0}\",\"url\":\"{1}\"}}", urlName, url);
//        }
//    }

//    ConvertedJSON += "]";

//    return ConvertedJSON;
//}