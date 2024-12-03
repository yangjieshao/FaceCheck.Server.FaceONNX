namespace FaceCheck.Server.Model
{
    /// <summary>
    /// </summary>
    public class HttpResult
    {
        /// <summary>
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// </summary>
        public int Code {  get; set; }
    }
    /// <summary>
    /// </summary>
    public class HttpResult<T> : HttpResult
    {
        /// <summary>
        /// </summary>
        public T Data { get; set; }
    }
}
