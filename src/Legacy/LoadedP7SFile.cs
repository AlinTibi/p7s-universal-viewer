namespace P7SExtractor
{
    public class LoadedP7SFile
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Size { get; set; }
        public string Signature { get; set; } = Localization.T("Necunoscuta", "Unknown");
    }
}
