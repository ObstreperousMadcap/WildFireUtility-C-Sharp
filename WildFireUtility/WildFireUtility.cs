﻿// Copyright (c) 2023 Michael Logan <ObstreperousMadcap@soclab.tech>
//
// Permission to use, copy, modify, and distribute this software for any
// purpose with or without fee is hereby granted, provided that the above
// copyright notice and this permission notice appear in all copies.
//
// THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
// WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
// MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
// ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
// WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
// ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
// OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.


/*
To-Do List 
 - Add more comments to explain the flow
 - Wrap error handling around file access - try/catch
 - Confirm actual text of apiErrorCodes[] - contact WildFire engineering
 - Add support for /get/report/
 - Add CLI option for different region URL
 - Add CLI option for using submit logfile to obtain verdicts
 - Add CLI option for using submit logfile to obtain reports
*/

// Install System.CommandLine from command prompt:
//     dotnet add package System.CommandLine --prerelease

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using Formatting = Newtonsoft.Json.Formatting;

namespace WildFireUtility;

internal class Program
{
    internal static void Main(string[] args)
    {
        // Parse the command line parameters.
        Dictionary<string, string> cliArguments = ParseCommandLine(args);

        // Initialize variables that hold API results used for command-line output and toi build logfile content.
        Dictionary<string, Dictionary<string, string>> apiResult; // Holds result from a single API call.
        Dictionary<string, Dictionary<string, string>> apiResults =
            new Dictionary<string, Dictionary<string, string>>(); // Holds results from all API calls.

        // Validate the arguments and call the WildFire API.
        switch (cliArguments["apiOption"])
        {
            case string apiOptionMatch when apiOptionMatch.Equals("submitFile"):
                if (File.Exists(cliArguments["value"]))
                {
                    // Submit the file.
                    apiResult = CallWildFireAPI(cliArguments["apiKey"], cliArguments["apiOption"],
                        cliArguments["value"]).Result;
                    // Save the results.
                    apiResults.Add(apiResult.ElementAt(0).Key, apiResult.ElementAt(0).Value);
                }
                else
                    // Add all of the standard keys with empty values to ensure proper CSV data alignment.
                    apiResults.Add(cliArguments["value"],
                        new Dictionary<string, string>
                        {
                            { "url", "" },
                            { "filename","" },
                            { "sha256","" },
                            { "md5", "" },
                            { "size", "" },
                            { "filetype", "" },
                            { "Status", "Parameter Error; File not found." } // What went wrong.
                        });
                break;

            case string apiOptionMatch when apiOptionMatch.Equals("submitFiles"):
                // Submit each of the files in the folder
                if (Directory.Exists(cliArguments["value"]))
                    // Interate through all of the files in the folder.
                    foreach (string folderFile in Directory.EnumerateFiles(cliArguments["value"]))
                    {
                        // Submit the file.
                        apiResult = CallWildFireAPI(cliArguments["apiKey"], cliArguments["apiOption"], folderFile)
                            .Result;
                        // Save the results.
                        apiResults.Add(apiResult.ElementAt(0).Key, apiResult.ElementAt(0).Value);
                    }
                else
                    // Add all of the standard keys with empty values to ensure proper CSV data alignment.
                    apiResults.Add(cliArguments["value"],
                        new Dictionary<string, string>
                        {
                            { "url", "" },
                            { "filename", "" },
                            { "sha256","" },
                            { "md5", "" },
                            { "size", "" },
                            { "filetype", "" },
                            { "Status", "Parameter Error; Folder not found." } // What went wrong.
                        });
                break;

            case string apiOptionMatch when apiOptionMatch.Equals("submitLink"):
                if (CheckLinkFormat(cliArguments["value"])) // Validate the link is properly formatted.
                {
                    // Submit the link.
                    apiResult = CallWildFireAPI(cliArguments["apiKey"], cliArguments["apiOption"],
                        cliArguments["value"]).Result;
                    // Save the results.
                    apiResults.Add(apiResult.ElementAt(0).Key, apiResult.ElementAt(0).Value);
                }
                else
                    // Add all of the standard keys with empty values to ensure proper CSV data alignment.
                    apiResults.Add(cliArguments["value"],
                        new Dictionary<string, string>
                        {
                            { "url", "" },
                            { "sha256","" },
                            { "md5", "" },
                            { "Status", "Parameter Error; Invalid link format." } // What went wrong.
                        });
                break;

            case string apiOptionMatch when apiOptionMatch.Equals("submitLinks"):
                if (File.Exists(cliArguments["value"]))
                    // Open the file.
                    using (StreamReader reader = new StreamReader(cliArguments["value"]))
                    {
                        string link;
                        // Iterate through every line in the file.
                        while ((link = reader.ReadLine()) != null)
                            if (CheckLinkFormat(link)) // Validate the link is properly formatted.
                            {
                                // Submit the link.
                                apiResult = CallWildFireAPI(cliArguments["apiKey"], cliArguments["apiOption"], link)
                                    .Result;
                                // Save the results.
                                apiResults.Add(apiResult.ElementAt(0).Key, apiResult.ElementAt(0).Value);
                            }
                            else
                                // Add all of the standard keys with empty values to ensure proper CSV data alignment.
                                apiResults.Add(link,
                                    new Dictionary<string, string>
                                    {
                                        { "url", "" },
                                        { "sha256","" },
                                        { "md5", "" },
                                        { "Status", "Parameter Error; Invalid link format." } // What went wrong.
                                    });
                    }
                else
                    // Add all of the standard keys with empty values to ensure proper CSV data alignment.
                    apiResults.Add(cliArguments["value"],
                        new Dictionary<string, string>
                        {
                            { "url", "" },
                            { "sha256","" },
                            { "md5", "" },
                            { "Status", "Parameter Error; File not found." } // What went wrong.
                        });
                break;

            case string apiOptionMatch when apiOptionMatch.Equals("verdictHash"):
                if (CheckHashFormat(cliArguments["value"])) // Validate the hash is properly formatted.
                {
                    // Get the verdict for the hash.
                    apiResult = CallWildFireAPI(cliArguments["apiKey"], cliArguments["apiOption"],
                        cliArguments["value"]).Result;
                    // Save the results.
                    apiResults.Add(apiResult.ElementAt(0).Key, apiResult.ElementAt(0).Value);
                }
                else
                    // Add all of the standard keys with empty values to ensure proper CSV data alignment.
                    apiResults.Add(cliArguments["value"],
                        new Dictionary<string, string>
                        {
                            { "sha256", "" },
                            { "verdict", "" },
                            { "md5", "" },
                            { "Status", "Parameter Error; Invalid hash format." } // What went wrong.
                        });
                break;

            case string apiOptionMatch when apiOptionMatch.Equals("verdictHashes"):
                if (File.Exists(cliArguments["value"]))
                    // Open the file.
                    using (StreamReader reader = new StreamReader(cliArguments["value"]))
                    {
                        string hash;
                        // Iterate through every line in the file.
                        while ((hash = reader.ReadLine()) != null)
                            if (CheckHashFormat(hash)) // Validate the hash is properly formatted.
                            {
                                // Get the verdict for the hash.
                                apiResult = CallWildFireAPI(cliArguments["apiKey"], cliArguments["apiOption"], hash)
                                    .Result;
                                // Save the results.
                                apiResults.Add(apiResult.ElementAt(0).Key, apiResult.ElementAt(0).Value);
                            }
                            else
                                // Add all of the standard keys with empty values to ensure proper CSV data alignment.
                                apiResults.Add(hash,
                                    new Dictionary<string, string>
                                    {
                                        { "sha256", "" },
                                        { "verdict", "" },
                                        { "md5", "" },
                                        { "Status", "Parameter Error; Invalid hash format." } // What went wrong.
                                    });
                    }
                else
                    // Add all of the standard keys with empty values to ensure proper CSV data alignment.
                    apiResults.Add(cliArguments["value"],
                        new Dictionary<string, string>
                        {
                            { "sha256", "" },
                            { "verdict","" },
                            { "md5", "" },
                            { "Status", "Parameter Error; Folder not found." } // What went wrong.
                        });
                break;

            case string apiOptionMatch when apiOptionMatch.Equals("verdictLink"):
                if (CheckLinkFormat(cliArguments["value"])) // Validate the link is properly formatted.
                {
                    // Get the verdict for the link.
                    apiResult = CallWildFireAPI(cliArguments["apiKey"], cliArguments["apiOption"],
                        cliArguments["value"]).Result;
                    // Save the results.
                    apiResults.Add(apiResult.ElementAt(0).Key, apiResult.ElementAt(0).Value);
                }
                else
                    // Add all of the standard keys with empty values to ensure proper CSV data alignment.
                    apiResults.Add(cliArguments["value"],
                        new Dictionary<string, string>
                        {
                            { "url", "" },
                            { "verdict", "" },
                            { "analysis_time", "" },
                            { "valid", "" },
                            { "Status", "Parameter Error; Invalid link format." } // What went wrong.
                        });
                break;

            case string apiOptionMatch when apiOptionMatch.Equals("verdictLinks"):
                if (File.Exists(cliArguments["value"]))
                    // Open the file.
                    using (StreamReader reader = new StreamReader(cliArguments["value"]))
                    {
                        string link;
                        // Iterate through every line in the file.
                        while ((link = reader.ReadLine()) != null)
                            if (CheckLinkFormat(link)) // Validate the link is properly formatted.
                            {
                                // Get the verdict for the link
                                apiResult = CallWildFireAPI(cliArguments["apiKey"], cliArguments["apiOption"], link)
                                    .Result;
                                // Save the results.
                                apiResults.Add(apiResult.ElementAt(0).Key, apiResult.ElementAt(0).Value);
                            }
                            else
                                // Add all of the standard keys with empty values to ensure proper CSV data alignment.
                                apiResults.Add(link,
                                    new Dictionary<string, string>
                                    {
                                        { "url", "" },
                                        { "verdict", "" },
                                        { "analysis_time", "" },
                                        { "valid", "" },
                                        { "Status", "Parameter Error; Invalid link format." } // What went wrong.
                                    });
                    }
                else
                    // Add all of the standard keys with empty values to ensure proper CSV data alignment.
                    apiResults.Add(cliArguments["value"],
                        new Dictionary<string, string>
                        {
                            { "url", "" },
                            { "verdict", "" },
                            { "analysis_time", "" },
                            { "valid", "" },
                            { "Status", "Parameter Error; Folder not found." } // What went wrong.
                        });
                break;
        }

        // Initialize variables used to build the content for the logfile.
        string logfileHeader = ""; // Contains a unique key for each column header.
        List<string> logfileEntries = new List<string>(); // Holds results from all API calls.

        // Display the results in the command shell; build the logfile content.
        for (int outerElement = 0; outerElement < apiResults.Count; outerElement++)
        {
            Console.WriteLine("Parameter: " + apiResults.ElementAt(outerElement).Key);
            // Build the logfile header; A conditional is necessary to account for the command-line options.
            // that result in multiple API calls and multiple instances of the outer key, "Parameter".
            if (!logfileHeader.Contains("Parameter")) // The key is needed only once in the header.
                logfileHeader = "Parameter";
            string logfileEntry = apiResults.ElementAt(outerElement).Key; // Add the key, e.g. the command-line hash or link.
            for (int innerElement = 0; innerElement < apiResults.ElementAt(outerElement).Value.Count; innerElement++)
            {
                // Use temporary variables for the key and value to streamline the output and logfile content creation.
                string innerElementKey = apiResults.ElementAt(outerElement).Value.ElementAt(innerElement).Key;
                string innerElementValue = apiResults.ElementAt(outerElement).Value.ElementAt(innerElement).Value;
                Console.WriteLine("\t" + innerElementKey + ": " + innerElementValue);
                // Build the logfile header; A conditional is necessary to account for the command-line options.
                // that result in multiple API calls and multiple inner keys. (e.g., "sha256", "verdict", etc.).
                if (!logfileHeader.Contains(innerElementKey)) // The key is needed only once in the header.
                    logfileHeader = logfileHeader + "," + innerElementKey;
                logfileEntry = logfileEntry + "," + innerElementValue; // Add a comma separator and the next value from the results.
            }
            Console.WriteLine(); // Blank line to improve readability.
            logfileEntries.Add(logfileEntry); // Save the entry; will be saved to logfile later.
        }

        // The logfile name is comprised of the datetime and the commnand-line option.
        string logfileName = DateTime.Now.ToString("yyyyMMddHHmmss") + "-" + cliArguments["apiOption"] + ".csv";

        // The logfile will be stored in a subfolder of the folder where this app is executing.
        // Need to use AppContext.BaseDirectory because 'System.Reflection.AssemblyName.CodeBase'
        // always returns an empty string for assemblies embedded in a single-file app.
        string logfilePath = AppContext.BaseDirectory + "logfiles"; 
        if (!Directory.Exists(logfilePath))
            Directory.CreateDirectory(logfilePath);

        // Save the results.
        File.WriteAllText(Path.Combine(logfilePath, logfileName), logfileHeader + "\r\n"); 
        File.AppendAllLines(Path.Combine(logfilePath, logfileName), logfileEntries);

        // Let the user know the name/location of the logfolder.
        Console.WriteLine("Results saved to " + Path.Combine(logfilePath, logfileName));
    }

    public static Dictionary<string, string> ParseCommandLine(string[] args)
    {
        // For more information about System.CommandLine:
        // https://learn.microsoft.com/en-us/dotnet/standard/commandline/get-started-tutorial

        Dictionary<string,string> cliArguments = new Dictionary<string, string>
        {
            { "apiKey", "" },
            {
                "apiOption", ""
            }, // submitFile, submitFiles, submitLink, submitLinks, verdictHash, verdictHashes, verdictLink, verdictLinks
            { "value", "" }
        };

        Option<string?> apiKey = new Option<string?>(
                name: "--apikey",
                description: "WildFire API Key")
            { ArgumentHelpName = "APIKEY" };
        apiKey.IsRequired = true;
        apiKey.AddValidator(result =>
        {
            string apiKeySubmitted = result.GetValueForOption(apiKey).ToString();
            if (apiKeySubmitted.All(char.IsLetterOrDigit) && apiKeySubmitted.Length == 64)
                cliArguments["apiKey"] = apiKeySubmitted;
            else
                result.ErrorMessage = "<APIKEY> has an incorrect length and/or contains invalid characters.";
        });

        Option<FileInfo?> submitFile = new Option<FileInfo?>(
                name: "--file",
                description: "Submit <FILE>")
            { ArgumentHelpName = "FILE" };
        submitFile.AddValidator(result =>
        {
            cliArguments["apiOption"] = "submitFile";
            cliArguments["value"] = result.GetValueForOption(submitFile).FullName;
            ;
        });

        Option<FileInfo?> submitFiles = new Option<FileInfo?>(
                name: "--files",
                description: "Submit the file(s) in <FOLDER>")
            { ArgumentHelpName = "FOLDER" };
        submitFiles.AddValidator(result =>
        {
            cliArguments["apiOption"] = "submitFiles";
            cliArguments["value"] = result.GetValueForOption(submitFiles).FullName;
        });

        Option<string?> submitLink = new Option<string?>(
                name: "--link",
                "Submit <LINK>")
            { ArgumentHelpName = "LINK" };
        submitLink.AddValidator(result =>
        {
            cliArguments["apiOption"] = "submitLink";
            cliArguments["value"] = result.GetValueForOption(submitLink).ToString();
        });

        Option<FileInfo?> submitLinks = new Option<FileInfo?>(
                name: "--links",
                description: "Submit the link(s) in <FILE>")
            { ArgumentHelpName = "FILE" };
        submitLinks.AddValidator(result =>
        {
            cliArguments["apiOption"] = "submitLinks";
            cliArguments["value"] = result.GetValueForOption(submitLinks).FullName;
        });

        Option<string?> verdictHash = new Option<string?>(
                name: "--hash",
                description: "Obtain the verdict for MD5/SHA-256 <HASH>")
            { ArgumentHelpName = "HASH" };
        verdictHash.AddValidator(result =>
        {
            cliArguments["apiOption"] = "verdictHash";
            cliArguments["value"] = result.GetValueForOption(verdictHash).ToString();
        });

        Option<FileInfo?> verdictHashes = new Option<FileInfo?>(
                name: "--hashes",
                description: "Obtain the verdict for MD5/SHA-256 hash(es) in <FILE>")
            { ArgumentHelpName = "FILE" };
        verdictHashes.AddValidator(result =>
        {
            cliArguments["apiOption"] = "verdictHashes";
            cliArguments["value"] = result.GetValueForOption(verdictHashes).FullName;
        });

        Option<string?> verdictLink = new Option<string?>(
                name: "--link",
                description: "Obtain the verdict for <LINK>")
            { ArgumentHelpName = "LINK" };
        verdictLink.AddValidator(result =>
        {
            cliArguments["apiOption"] = "verdictLink";
            cliArguments["value"] = result.GetValueForOption(verdictLink).ToString();
        });

        Option<FileInfo?> verdictLinks = new Option<FileInfo?>(
                name: "--links",
                description: "Obtain the verdict for link(s) in <FILE>")
            { ArgumentHelpName = "FILE" };
        verdictLinks.AddValidator(result =>
        {
            cliArguments["apiOption"] = "verdictLinks";
            cliArguments["value"] = result.GetValueForOption(verdictLinks).FullName;
        });

        Command? submit = new Command("submit", "Submit file(s)/link(s) to WildFire for analysis");
        submit.AddOption(apiKey);
        submit.AddOption(submitFile);
        submit.AddOption(submitFiles);
        submit.AddOption(submitLink);
        submit.AddOption(submitLinks);

        Command? verdict = new Command("verdict", "Obtain the verdict for file(s)/link(s)");
        verdict.AddOption(apiKey);
        verdict.AddOption(verdictHash);
        verdict.AddOption(verdictHashes);
        verdict.AddOption(verdictLink);
        verdict.AddOption(verdictLinks);

        Command? rootCommand = new RootCommand("WildFire API Utility");
        rootCommand.AddCommand(submit);
        rootCommand.AddCommand(verdict);
        rootCommand.InvokeAsync(args);

        return cliArguments;
    }

    public static async Task<Dictionary<string, Dictionary<string, string>>>
        CallWildFireAPI(string apiKey, string apiOption, string apiOptionValue,
            string apiHost = "https://wildfire.paloaltonetworks.com/")
    {
        // Initialize Variables
        HttpClient? httpClient = new HttpClient(); // Using a shared client for simplicity and speed.
        httpClient.BaseAddress = new Uri(apiHost); // To-Do: Turn this into a command-line option.

        // Possible error codes returned by the API.
        // Both numeric and text keys are used because the
        // responses have not been confirmed with engineering.
        Dictionary<string, string> apiErrorCodes = new Dictionary<string, string>
        {
            { "OK", "200; Successful call." },
            { "Unauthorized", "401; Invalid API key. Ensure that the API key is correct." },
            { "401", "Unauthorized; Invalid API key. Ensure that the API key is correct." },
            { "Forbidden", "403; Permission denied." },
            { "403", "Forbidden; Permission denied." },
            { "NotFound", "404; The file or report was not found." },
            { "404", "NotFound; The file or report was not found." },
            { "MethodNotAllowed", "405; Invalid request method." },
            { "405", "MethodNotAllowed; Invalid request method." },
            { "RequestEntityTooLarge", "413; File size over maximum limit." },
            { "413", "RequestEntityTooLarge; File size over maximum limit." },
            { "UnsupportedFileType", "418; File type is not supported." },
            { "418", "UnsupportedFileType; File type is not supported." },
            { "MaxRequestReached", "419; The maximum number of uploads per day has been exceeded." },
            { "419", "MaxRequestReached; The maximum number of uploads per day has been exceeded. " },
            { "InsufficientArguments", "420; Ensure the request has the required request parameters." },
            { "420", "InsufficientArguments; Ensure the request has the required request parameters." },
            { "MisdirectedRequest", "421; Invalid arguments. Ensure the request is properly constructed." },
            { "InvalidArgument", "421; Ensure the request is properly constructed." },
            { "421", "InvalidArgument; Ensure the request is properly constructed." },
            { "UnprocessableEntity", "422; The provided file or URL cannot be processed." },
            { "422", "UnprocessableEntity; The provided file or URL cannot be processed." },
            { "InternalError", "500; Internal error." },
            { "500", "InternalError; Internal error." },
            { "513", "513; File upload failed." }
        };

        // Map the command line options to the API endpoints.
        Dictionary<string, string> apiResourceURLs = new Dictionary<string, string>
        {
            { "submitFile", "publicapi/submit/file" },
            { "submitFiles", "publicapi/submit/file" },
            { "submitLink", "publicapi/submit/link" },
            { "submitLinks", "publicapi/submit/link" },
            { "verdictHash", "publicapi/get/verdict" },
            { "verdictHashes", "publicapi/get/verdict" },
            { "verdictLink", "publicapi/get/verdict" },
            { "verdictLinks", "publicapi/get/verdict" }
        };

        // Map the command line options to the response tags.
        // THe tags are used to extract the core API response content.
        Dictionary<string, string> apiResourceTags = new Dictionary<string, string>
        {
            { "submitFile", "upload-file-info" },
            { "submitFiles", "upload-file-info" },
            { "submitLink", "submit-link-info" },
            { "submitLinks", "submit-link-info" },
            { "verdictHash", "get-verdict-info" },
            { "verdictHashes", "get-verdict-info" },
            { "verdictLink", "get-verdict-info" },
            { "verdictLinks", "get-verdict-info" }
        };

        // Map the verdict codes to text so the output is more meaningful.
        Dictionary<string, string> apiVerdictCodes = new Dictionary<string, string>
        {
            { "0", "Benign" },
            { "1", "Malware" },
            { "2", "Grayware" },
            { "4", "Phishing" },
            { "5", "C2" },
            { "-100", "Pending; the file exists, but there is currently no verdict." },
            { "-101", "Error" },
            { "-102", "Unknown; Cannot find file record in the database." },
            { "-103", "Invalid hash value." }
        };

        // Contains all of the API parameter names and values.
        MultipartFormDataContent multipartFormDataContent = new MultipartFormDataContent();

        // Dictionary containing final content returned to caller.
        Dictionary<string, Dictionary<string, string>> apiResultComplete =
            new Dictionary<string, Dictionary<string, string>>();

        // Use apiOptionValue as the key to ensure the dictionary returned to the caller is unique.
        apiResultComplete.Add(apiOptionValue, new Dictionary<string, string>());

        // Add the API key to the form.
        multipartFormDataContent.Add(new StringContent(apiKey), @"""apikey""");

        // Tailor the rest of the content to match the command-line option.
        switch (apiOption)
        {
            case string apiOptionMatch
                when apiOptionMatch.Equals("submitFile") || apiOptionMatch.Equals("submitFiles"):
                // Load and add the file to the form
                string fileName = Path.GetFileName(apiOptionValue);
                StreamContent fileStreamContent = new StreamContent(File.OpenRead(apiOptionValue));
                fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                fileStreamContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                {
                    Name = @"""file""",
                    FileName = @$"""{fileName}"""
                };
                multipartFormDataContent.Add(fileStreamContent);
                break;

            case string apiOptionMatch
                when apiOptionMatch.Equals("submitLink") || apiOptionMatch.Equals("submitLinks"):
                multipartFormDataContent.Add(new StringContent(apiOptionValue), @"""link""");
                break;

            case string apiOptionMatch
                when apiOptionMatch.Equals("verdictHash") || apiOptionMatch.Equals("verdictHashes"):
                // Add the hash to the form
                multipartFormDataContent.Add(new StringContent(apiOptionValue), @"""hash""");
                break;

            case string apiOptionMatch
                when apiOptionMatch.Equals("verdictLink") || apiOptionMatch.Equals("verdictLinks"):
                multipartFormDataContent.Add(new StringContent(apiOptionValue), @"""url""");
                break;
        }

        // Make the API call with the key and form-encoded parameters.
        HttpResponseMessage apiResultHTTP = await httpClient.PostAsync(apiResourceURLs[apiOption], multipartFormDataContent);

        // Did it fail?
        if (apiResultHTTP.IsSuccessStatusCode)
        {
            // Get the results.
            XmlDocument apiResultXML = new XmlDocument();
            apiResultXML.LoadXml(await apiResultHTTP.Content.ReadAsStringAsync());

            // Remove the XML declaration node because JsonConvert.DeserializeObject barfs on "?xml".
            foreach (XmlNode node in apiResultXML)
                if (node.NodeType == XmlNodeType.XmlDeclaration)
                    apiResultXML.RemoveChild(node);

            // Convert the API response to JSON.
            // "omitRootObject: true" removes the "wildfire" outer key.
            string apiResultJSON = JsonConvert.SerializeXmlNode(apiResultXML, Formatting.None, true);

            // Convert the JSON to a dictionary.
            var apiResultCoreContent =
                JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(apiResultJSON);

            // Extract and save just the core API response content.
            foreach (KeyValuePair<string, string> dictElement in apiResultCoreContent[apiResourceTags[apiOption]])
                if (dictElement.Key == "verdict")
                    apiResultComplete[apiOptionValue].Add(dictElement.Key, apiVerdictCodes[dictElement.Value]);
                else
                    apiResultComplete[apiOptionValue].Add(dictElement.Key, dictElement.Value);
        }

        // Add the HTTP response status code.
        apiResultComplete[apiOptionValue].Add("Status", apiErrorCodes[apiResultHTTP.StatusCode.ToString()]);

        return apiResultComplete;
    }

    public static bool CheckHashFormat(string hash)
    {
        string hashPattern = "[A-F0-9]"; // Valid hash characters.
        Regex regexProcessor = new Regex(hashPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase); 
        if ((hash.Length == 32 || hash.Length == 64) && regexProcessor.IsMatch(hash))
            return true; // Valid MD5 or SHA-256 characters and length.
        return false; // Characters and/or length are invalid.
    }

    public static bool CheckLinkFormat(string URL)
    {
        string urlPattern = @"^(?:http(s)?:\/\/)?[\w.-]+(?:\.[\w\.-]+)+[\w\-\._~:/?#[\]@!\$&'\(\)\*\+,;=.]+$"; // Valid URL format and characters.
        Regex regexProcessor = new Regex(urlPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        return regexProcessor.IsMatch(URL);
    }
}