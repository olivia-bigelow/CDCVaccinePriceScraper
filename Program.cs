using System.Security.Authentication;
using HtmlAgilityPack;
using IronXL;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Numerics;

namespace CDCVaccinePriceScraper
{
    //NOTES ON WHAT DO FINISH
    //write data to excel files
    //check cleaning on data


    //april 1st 2013 missing data to be resolved manually

    /// <summary>
    /// this program is used to scracpe the cdc website for vaccine prices 
    /// https://www.cdc.gov/vaccines/programs/vfc/awardees/vaccine-management/price-list/archive.html
    /// and write those prices to an appropriate excel file
    /// </summary>
    internal class Program
    {
        static void Main(string[] args)
        {
            //scrape the website for links
            List<VaxSite> list = getUrls("https://www.cdc.gov/vaccines/programs/vfc/awardees/vaccine-management/price-list/archive.html");
            foreach (VaxSite site in list)
            {
                //if (site.date.Substring(site.date.Length - 2).Equals("03"))
                //{
                    ScrapeSite(site);
                    //write data to appropriate excel files
                    generateExcelFile(site);
                //}
            }
            
            Console.WriteLine("all done :3");
        }



        static void generateExcelFile(VaxSite site)
        {
            //setup file
            WorkBook workBook = WorkBook.Create(ExcelFileFormat.XLSX);
            workBook.Metadata.Title = site.url;

            foreach(Table t in site.tables)
            {
                WorkSheet workSheet;
                try
                {
                    workSheet = workBook.CreateWorkSheet(t.Title);
                }
                catch
                {
                    //influenza wasn't added properly in the table captions of may 1st 2015
                    string[] temp = t.Title.Split(" ");
                    string title = temp[0] + " influenza " + temp[1];
                    workSheet = workBook.CreateWorkSheet(title);
                }
                //add headers
                workSheet["A" +1].Value = "Vaccine";
                workSheet["B" + 1].Value = "BrandName";
                workSheet["C" + 1].Value = "NDC";
                workSheet["D"+ 1].Value = "Packaging";
                workSheet["E" + 1].Value = "CdcCost";
                workSheet["F" + 1].Value = "Private Sector Cost";
                workSheet["G" + 1].Value = "Contract End";
                workSheet["H" + 1].Value = "Manufacturer";
                workSheet["I" +  1].Value = "Contract Number";
                for (int i = 0; i < t.Vaxxes.Count; i++)
                {
                    workSheet["A" + (i + 2)].Value = t.Vaxxes[i].Vaccine;
                    workSheet["B" + (i + 2)].Value = t.Vaxxes[i].BrandName;
                    workSheet["C" + (i + 2)].Value = t.Vaxxes[i].NDC;
                    workSheet["D" + (i + 2)].Value = t.Vaxxes[i].Packaging;
                    workSheet["E" + (i + 2)].Value = t.Vaxxes[i].CdcCost;
                    workSheet["F" + (i + 2)].Value = t.Vaxxes[i].PrivateSectorCost;
                    workSheet["G" + (i + 2)].Value = t.Vaxxes[i].ContractEnd;
                    workSheet["H" + (i + 2)].Value = t.Vaxxes[i].Manufacturer;
                    workSheet["I" + (i + 2)].Value = t.Vaxxes[i].ContractNumber;
                }
                    
            }
            //format the data properly
            string date = site.date;
            date = date.Replace("/", "_");
            //write to file
            workBook.SaveAs($@"M:\divin\ReposHard\CDCVaccinePriceScraper\Files\Vaccine_"+date+".xlsx");
        }


        /// <summary>
        /// this method takes a vaxsite object, and scrapes the tables inside it. 
        /// This method does not return anything, but rather, populates the tables field inside the 
        /// site object. 
        /// </summary>
        /// <param name="site"></param>
        static void ScrapeSite(VaxSite site)
        {
            HtmlDocument doc = GetDocument(site.url);
            foreach (HtmlNode node in doc.DocumentNode.SelectNodes("//table"))
            {

                //extract table data
                List<List<string>> table = node
                .Descendants("tr")
                .Skip(0)
                .Where(tr => tr.Elements("th").Count() > 0)
                .Select(tr => tr.Elements("th").Select(th => th.InnerText.Trim()).ToList())
                .ToList();
                List<List<string>> table2 = node
               .Descendants("tr")
               .Skip(0)
               .Where(tr => tr.Elements("td").Count() > 1)
               .Select(tr => tr.Elements("td").Select(td => td.InnerText.Trim()).ToList())
               .ToList();


                //merge the data
                Table t = MergeData(table, table2, refineTitle(node.InnerText), site.date);

                //add it to the VaxSite Object
                site.tables.Add(t);

            }
        }

        static Table MergeData(List<List<string>> dat1, List<List<string>> dat2, string Title, string date) 
        {
            //build the table
            Table ret = new();
            //set title
            ret.Title = Title;
            //get the header
            ret.headers = dat1[0];
            //get flags for building the vaccine listing
            bool[] flags = headerFlags(ret);
            //populate data
            int dat1index = 1;
            foreach(List<string> d in dat1)
            {
                cleanData(d);
            }
            VaccineListing prev = null;
            //if its between 2010 and 2002, you need to build a list of vaccines, otherwise, build a single vaccine
            int year = int.Parse(date.Substring(date.Length - 2));
            if (year < 11 && year > 1)
            {
                List<VaccineListing> temp;
                foreach (List<string> data2 in dat2)
                {
                    cleanData(data2);
                    //build a vaccine
                    if (dat1.Count < 2)
                        temp = buildvaxxes(flags, data2, new List<string>());
                    else
                        temp = buildvaxxes(flags, data2, dat1[dat1index]);
                    dat1index++;
                    foreach (VaccineListing v in temp)
                        ret.Vaxxes.Add(v);
                }
            }
            else
            {
                //BUIILD A SINGLE VACCINE LISTING
                foreach (List<string> data2 in dat2)
                {
                    cleanData(data2);
                    VaccineListing temp;
                    bool tick = buildvax(flags, data2, prev, dat1[dat1index], out temp);
                    //if (tick)
                        dat1index++;
                    ret.Vaxxes.Add(temp);
                    prev = temp;

                }
            }
            return ret;
        }

        static string removeRegandTrademark(string s)
        {
            s = s.Replace("&trade;", "");
            s = s.Replace("&reg;", "");
            return s;
        }

        static string fixDash(string s)
        {
            s = s.Replace("&ndash;", "-");
            s = s.Replace("&nbsp;", "");
            s = s.Replace("&times;", "");
            s = s.Replace("&ldquo;", "");
            s = s.Replace("&rdquo;", "");
            s = s.Replace("&amp;", "");
            return s;
        }
        static string cleanData(string data)
        {
            data = removeRegandTrademark(data);
            data = fixDash(data);
            return data;
        }

        static void cleanData(List<string> data)
        {
            for(int i = 0; i < data.Count; i++)
            {
                data[i] = cleanData(data[i]);
            }
        }


        /// <summary>
        /// this method will build a vaccine given the input parameters
        /// </summary>
        /// <param name="flags"></param> an array of boolean flags determining what fields need to be populated
        /// <param name="data2"></param> a list of tr elements
        /// <param name="prev"></param> another vaccine listing to fill out missing data
        /// <param name="data1"></param> a list of th elements
        /// <param name="vax"></param> the vaccinelisting object built by this method
        /// <returns></returns> true if the previous vaccine listing is not used in construction, false otherwise
        static bool buildvax(bool[]flags, List<string> data2, VaccineListing prev, List<string> data1, out VaccineListing vax)
        {
            //determine if prev is necessary
            int boolcount = 0;
            foreach (bool b in flags)
                if (b)
                    boolcount++;
            bool needPrev = false;
            if (boolcount != data1.Count + data2.Count)
                needPrev = true;

            //initialize the vaccinelisting
            vax = new();

            //append the data
            List<string>data = data1.Concat(data2).ToList();
            if(needPrev)
            {
                data.Insert(0, prev.Vaccine);
                if (int.TryParse(data[1].Substring(0,1), out int res))
                    data.Insert(1, prev.BrandName); 
                data.Add(prev.ContractEnd);
                data.Add(prev.Manufacturer);
                data.Add(prev.ContractNumber);
            }
            int datind = 0;

            //iterate through the flags, adding neccessary fields
            for(int i = 0; i < flags.Count(); i++)
                if (flags[i])
                {
                    vax.addParam(i, data[datind]);
                    datind++;
                }
            return needPrev;
        }
        static string removeFootNote(string s)
        {
            s = s.Replace("[1]", "");
            s = s.Replace("[2]", "");
            s = s.Replace("[3]", "");
            s = s.Replace("[4]", "");
            s = s.Replace("[5]", "");
            s = s.Replace("[6]", "");
            s = s.Replace("[7]", "");
            s = s.Replace("[5, 6]", "");
            s = s.Replace("/", "");
            s = s.Replace("&curren;", "");
            s = s.Replace("&bull;", "");
            s = s.Replace("-Hib", "");
            s = s.Replace("#", "");
            s = s.Replace("\n", "");
            return s;
        }

        //FIX THIS FIX THIS FIX THIS
        /// <summary>
        /// this method takes data and returns a list of vaccine listing objects from the parsed data
        /// </summary>
        /// <param name="flags"></param> an array of bools determining which fields are used in the vaccines
        /// <param name="data"></param> a list of td elements
        /// <param name="heads"></param> a list of th elements
        /// <returns></returns>
        public static List<VaccineListing> buildvaxxes(bool[] flags, List<string> tds, List<string> heads)
        {
            //initialize return
            List<VaccineListing> ret = new();

            //merge the data
            List<string> data = heads.Concat(tds).ToList();
            data[0] = removeFootNote(data[0]);
            //find the index of cdc cost
            int cdcCostIndex = 0;
            for(int i = 0; i<4; i++)
                if (flags[i])
                    cdcCostIndex++;
            //split the cdc costs by new lines to determine how many vaccine need to be made. 
            string[] costsTemp = data[cdcCostIndex].Split("$");
            string[]costs = new string[costsTemp.Length-1];
            for (int i = 1; i < costsTemp.Length; i++)
            {
                costs[i-1] = "$" + costsTemp[i].Trim();
            }
            //determine if splitting is needed.
            if(costs.Length > 1) 
            {
                //split it
                string[][] data2 = new string[data.Count][];
                for(int i= 0; i<data.Count; i++)
                {
                    if (data[i].Contains(costs[0]) && data[i].Contains(costs[1]))
                    {
                        data2[i] = costs;
                        continue;
                    }
                    string[] toInsert = new string[costs.Length];
                    string[] temp = data[i].Split("\n");
                    for(int j = 0; j<temp.Length; j++)
                    {
                        if (temp[j] == null)
                            temp[j] = temp[j-1];
                    }
                    //3 possible cases, 1 entry, cost.length entries, or more than cost.length entries
                    if (temp.Length == 1)
                        for (int j = 0; j < costs.Length; j++)
                            toInsert[j] = data[i];
                    else if (temp.Length == costs.Length)
                        toInsert = temp;
                    else
                    {
                        string tempstr = "";
                        int k = 0;
                        int ind = 0;
                        foreach(string str in temp)
                        {
                            //int.TryParse(str.Substring(str.Length - 1), out k)
                            if (!str.Substring(str.Length - 1).Equals("s") || !str.Substring(str.Length - 1).Equals("l"))
                            {
                                tempstr += (str + " ");
                            }
                            else
                            {
                                tempstr += str;
                                toInsert[ind] = tempstr;
                                ind++;
                                tempstr = "";
                            }

                        }
                        for(int j = 0; j < toInsert.Length;j++)
                        {
                            if (toInsert[j] == null)
                                toInsert[j]= tempstr;
                        }
                    }
                    data2[i] = toInsert;
                }
                //build vaccines from each row of the array
                for(int i = 0; i<costs.Length; i++)
                {
                    VaccineListing vax = new();
                    int ind = 0;
                    for(int j = 0; j<flags.Length; j++)
                        if (flags[j])
                        {
                            vax.addParam(j, data2[ind][i]);
                            ind++;
                        }
                    ret.Add(vax);
                }


            }
            else
            {
                //build a single vaccine and add it to the list
                VaccineListing vax = new();
                int ind = 0;
                for (int j = 0; j < flags.Length; j++)
                    if (flags[j])
                    {
                        vax.addParam(j, data[ind]);
                        ind++;
                    }
                ret.Add(vax);

            }
            return ret;
        }

        static bool[] headerFlags(Table t )
        {
            bool[] ret = new bool[9];
            for(int i = 0; i < 9; i++)
            {
                ret[i] = false;
            }
            foreach(string s in t.headers)
            {
                if (s.StartsWith("Vaccine"))
                {
                    ret[0] = true;
                    continue;
                }
                if (s.StartsWith("Brand"))
                {
                    ret[1] = true;
                    continue;
                }
                if (s.StartsWith("NDC"))
                {
                    ret[2] = true;
                    continue;
                }
                if (s.StartsWith("Packag"))
                {
                    ret[3] = true;
                    continue;
                }
                if (s.StartsWith("CDC"))
                {
                    ret[4] = true;
                    continue;
                }
                if (s.StartsWith("Private"))
                {
                    ret[5] = true;
                    continue;
                }
                if (s.StartsWith("Contract End"))
                {
                    ret[6] = true;
                    continue;
                }
                if (s.StartsWith("Manuf"))
                {
                    ret[7] = true;
                    continue;
                }
                if (s.StartsWith("Contract N"))
                {
                    ret[8] = true;
                    continue;
                }
            }
            return ret;

        }

        private static string refineTitle(string s)
        {
            //remove the \n from the beginning of the title
            s = s.Remove(0, 1);

            //remove "price list" and "/" from the title
            int indToRemove = s.IndexOf("Price List");
            s = s.Remove(indToRemove);
            string title = s.Replace("/", " ");
            return title;

        }


        /// <summary>
        /// this method will scrape the website given by url for urls given on the website of the tag li inside the cdc text block
        /// This method is not robust to inputs other than the one given in the top documentation.
        /// Do not expect accruate outputs for urls other than https://www.cdc.gov/vaccines/programs/vfc/awardees/vaccine-management/price-list/archive.html
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        static List<VaxSite> getUrls(string url)
        {
            //initialize input
            List<VaxSite> ret = new();

            //generate documnets
            HtmlDocument doc = GetDocument(url);
            //extract links
            foreach (HtmlNode node in doc.DocumentNode.SelectNodes("//ul"))
            {
                //if its in the wrong area, dismiss it
                if (!node.ParentNode.OuterHtml.StartsWith("<div class=\"cdc-textblock"))
                    continue;
             
                //get every url in the collection
                List<HtmlNode> urls = node.Descendants("li").ToList();
                foreach(HtmlNode node2 in urls)
                {
                    //do not etract current data
                    if (node2.InnerText.StartsWith("Current"))
                        continue;

                    //if its not curent build the Vaxsite object
                    ret.Add(new VaxSite(node2.InnerText, node2.InnerHtml));
                }
            }


            return ret;
        }

        /// <summary>
        /// This method takes a url and returns an htmlDocument object of that webpage.
        /// This method is assumes that the input is a valid URL and may not work properly without it. 
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        static HtmlDocument GetDocument(string url)
        {
            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(url);
            return doc;
        }

    }



    /// <summary>
    /// this class represents a single listing of a vaccine
    /// </summary>
    public class VaccineListing
    {
        public string Vaccine { get; set; }
        public string BrandName { get; set; }
        public string NDC { get; set; }
        public string Packaging { get; set; }
        public string CdcCost { get; set; }
        public string PrivateSectorCost { get; set; }
        public string ContractEnd { get; set; }
        public string Manufacturer { get; set; }
        public string ContractNumber { get; set; }
        public VaccineListing()
        {
            Vaccine = "";
            BrandName = string.Empty;
            NDC = string.Empty;
            Packaging = string.Empty;
            CdcCost = string.Empty;
            PrivateSectorCost = string.Empty;
            ContractEnd = string.Empty;
            Manufacturer = string.Empty;
            ContractNumber = string.Empty;
        }

        /// <summary>
        /// adds this given parameter to the vaccine listing given the index of the following mapping
        /// 0 -> vaccine, 1-> brandname, 2-> NDC, 3-> packaging, 4-> cdccost, 5-> private sector cost, 6-> contract end
        /// 7-> manufacturer, 8-> contract number
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        public void addParam(int index, string value)
        {
            if (index < 4)
            {
                if (index < 2)
                {
                    if (index == 0)
                    {
                        this.Vaccine = value;
                    }
                    else
                        this.BrandName = value;
                }
                else
                {
                    if (index == 3)
                        this.Packaging = value;
                    else
                        this.NDC = value;
                }
            }
            else
            {
                if (index < 7)
                {
                    if (index < 6)
                        if (index == 5)
                            this.PrivateSectorCost = value;
                        else
                            this.CdcCost = value;
                    else
                        this.ContractEnd = value;
                }
                else
                {
                    if (index == 7)
                        this.Manufacturer = value;
                    else
                        this.ContractNumber = value;
                }
            }
        }

    }

    /// <summary>
    /// this class represents a table of vaccine Listings. 
    /// </summary>
    public class Table
    {
        public List<VaccineListing> Vaxxes{ get; set; }
        public String Title { get; set; }
        public List<string> headers { get; set; }
        public Table()
        {
            Vaxxes = new();
            Title = "";
            headers = new();
        }
    }

    //this represents a single website of vaccine prices
    public class VaxSite
    {
        public string date;
        public string url;
        public List<Table> tables;
        public VaxSite(string d, string u)
        {
            date = d.Replace(" Vaccine Price List", "");
            u = u.Replace("<a href=\"", "");
            u = u.Replace(d, "");
            u = u.Replace("\"></a>", "");
            url = "https://www.cdc.gov" + u;
            tables = new();
        }
    }

}