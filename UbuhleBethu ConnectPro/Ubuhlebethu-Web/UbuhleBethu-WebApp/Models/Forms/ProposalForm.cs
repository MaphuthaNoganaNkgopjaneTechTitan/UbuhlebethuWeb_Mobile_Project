using System.ComponentModel.DataAnnotations;

namespace UbuhlebethuConnectPro.WebApp.Models.Forms
{
    /// <summary>The public "Submit proposal" form (personal details + business proposal upload).</summary>
    public class ProposalForm : IValidatableObject
    {
        public string? ProjectSlug { get; set; }

        [Required(ErrorMessage = "Please enter your full name."), StringLength(100, MinimumLength = 2, ErrorMessage = "Please enter your full name.")]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your email address."), EmailAddress(ErrorMessage = "That email address doesn't look right."), StringLength(150)]
        [Display(Name = "Email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter a phone number."), RegularExpression(@"^[0-9+()\-\s]{7,30}$", ErrorMessage = "Please enter a valid phone number (digits, spaces, + ( ) or - only).")]
        [Display(Name = "Phone number")]
        public string Phone { get; set; } = string.Empty;

        [StringLength(150)]
        [Display(Name = "Company / organisation")]
        public string? Company { get; set; }

        [Required(ErrorMessage = "Where will the work take place?"), StringLength(150)]
        [Display(Name = "Site location")]
        public string SiteLocation { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please choose a project type."), StringLength(100)]
        [Display(Name = "Project type")]
        public string ProjectType { get; set; } = string.Empty;

        [Range(0, 10_000_000_000, ErrorMessage = "Please enter a valid amount.")]
        [Display(Name = "Indicative budget (R)")]
        public decimal? Budget { get; set; }

        [Required(ErrorMessage = "Tell us a little about the work."), StringLength(2000, MinimumLength = 10, ErrorMessage = "Please write at least 10 characters.")]
        [Display(Name = "About the project")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Business proposal document")]
        public IFormFile? Document { get; set; }

        [Display(Name = "Consent")]
        public bool Consent { get; set; }

        /// <summary>Honeypot – hidden from people, filled in by bots.</summary>
        public string? Website { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!Consent)
                yield return new ValidationResult("Please tick the box to agree to how we use your information.", new[] { nameof(Consent) });
        }
    }

    public class TrackForm
    {
        [Required(ErrorMessage = "Enter the reference from your confirmation."), StringLength(40)]
        public string Reference { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter the email you used."), EmailAddress, StringLength(150)]
        public string Email { get; set; } = string.Empty;
    }
}
