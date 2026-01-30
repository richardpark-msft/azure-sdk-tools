using System.Text.Json.Serialization;


namespace Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec
{
    public class TspToolResponse : TypeSpecBaseResponse
    {
        [JsonPropertyName("is_successful")]
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// The raw command output from the tsp-client invocation.
        /// </summary>
        [JsonPropertyName("command_output")]
        public string? CommandOutput { get; set; }

        protected override string Format()
        {
            if (!IsSuccessful)
            {
                return string.Empty;
            }
            else
            {
                return string.Join(
                    Environment.NewLine,
                    [
                        $"### TypeSpec Project Path: {TypeSpecProject}",
                        string.Empty,
                        ..this.NextSteps ?? Enumerable.Empty<string>()
                    ]
                );
            }
        }
    }
}
