using Microsoft.AspNetCore.Mvc;
using SoftwareCenter.Core.UI;
using System.Threading.Tasks;

namespace SoftwareCenter.Host.Controllers
{
    public class MainController : Controller
    {
        private readonly IUiService _uiService;

        public MainController(IUiService uiService)
        {
            _uiService = uiService;
        }

        public async Task<IActionResult> Index()
        {
            // Instead of serving the static index.html directly,
            // we ask the UIManager to compose the zones.
            var composedHtml = await _uiService.GetComposedIndexPageAsync();
            return Content(composedHtml, "text/html");
        }
    }
}