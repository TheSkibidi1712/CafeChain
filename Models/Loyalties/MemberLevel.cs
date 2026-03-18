namespace CafeChain.Models.Loyalties
{
    public class MemberLevel
    {
        public int MemberId { get; set; }
        public string Name { get; set; }

        public int MinPoints { get; set; }
        public int? MaxPoints { get; set; }
    }
}
