using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TecMFS.Common.Configuration;
using TecMFS.Common.Constants;
using TecMFS.Common.Models;
using TecMFS.DiskNode.Models;

namespace TecMFS.DiskNode.Controllers
{
    [ApiController]
    [Route("api/status")]
    public class StatusController : ControllerBase
    {
        private readonly TecMFS.DiskNode.Models.NodeConfiguration _config;

        public StatusController(IOptions<TecMFS.DiskNode.Models.NodeConfiguration> config)
        {
            _config = config.Value;
        }

        [HttpGet("node")]
        public ActionResult<NodeStatus> GetNodeStatus()
        {
            var status = new NodeStatus
            {
                NodeId = _config.NodeId,
                IsOnline = true,
                UsedStorage = 0,
                TotalStorage = SystemConstants.MAX_NODE_STORAGE,
                BlockCount = 0
            };

            return Ok(status);
        }
    }
}

