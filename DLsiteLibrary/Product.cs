namespace DLsiteLibrary;

public class Product
{
    public string work_name { get; set; }
    public bool is_split_content { get; set; }
    public int content_count { get; set; }
    public Contents[] contents { get; set; }

    public class Contents
    {
        public string file_name { get; set; }
    }
}