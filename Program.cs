using System.Security.Authentication;
using HtmlAgilityPack;
using IronXL;
using System;
using System.Collections.Generic;

namespace CDCVaccinePriceScraper
{
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
                ScrapeSite(site);
            }
            //write data to appropriate excel files


            //stub for debugging
            Console.WriteLine("all done :3");
        }
        /// <summary>
        /// THIS METHOD IS NOT CURRENT RUNNING PROPERLY AND NEEDS TO FIXED TO ADEQUATLY SCRAPE DIFFERENLY FORMATTED PAGES
        /// This method parses the website given by URL and returns a list of lists of lists of strings. 
        /// this method is highly specialized and is meant to work for the following website
        /// https://www.cdc.gov/vaccines/programs/vfc/awardees/vaccine-management/price-list/index.html
        /// The returns is a list containing 2 2d lists, representing the entire table data. The tables of this website are
        /// split into 2 seperate terms th and td. so the first 2d list stores th and the second stores td
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        static List<List<List<string>>> getVaxNames(string url)
        {
            HtmlDocument doc = GetDocument(url);

            List<List<List<string>>> ret = new();
            foreach (HtmlNode node in doc.DocumentNode.SelectNodes("//table"))
            {
                List<List<string>> table = node
                .Descendants("tr")
                .Skip(1)
                .Where(tr => tr.Elements("th").Count() > 1)
                .Select(tr => tr.Elements("th").Select(th => th.InnerText.Trim()).ToList())
                .ToList();
                List<List<string>> table2 = node
               .Descendants("tr")
               .Skip(1)
               .Where(tr => tr.Elements("td").Count() > 1)
               .Select(tr => tr.Elements("td").Select(td => td.InnerText.Trim()).ToList())
               .ToList();


                ret.Add(table);
                ret.Add(table2);
            }
            return ret;
        }

        /// <summary>
        /// this method takes a vaxsite object, and scrapes the tables inside it. 
        /// This method does not return anything, but rather, populates the tables field inside the 
        /// site object. 
        /// </summary>
        /// <param name="site"></param>
        static void ScrapeSite(VaxSite site)
        {

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
            url = "cdc.gov"+u;
            tables = new();
        }
    }

}