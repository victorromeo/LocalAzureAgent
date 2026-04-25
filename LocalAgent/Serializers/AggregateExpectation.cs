namespace LocalAgent.Serializers
{
    public abstract class AggregateExpectation : IExpectation
    {
        /// <summary>
        /// Essentially a `kind` property - determines which child type to instantiate.
        /// </summary>
        public string SegmentWith { get; init; }
    }
}