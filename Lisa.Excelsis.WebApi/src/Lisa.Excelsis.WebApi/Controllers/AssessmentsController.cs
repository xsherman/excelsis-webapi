﻿using Microsoft.AspNet.Mvc;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Lisa.Excelsis.WebApi
{
    [Route("[controller]")]
    public class AssessmentsController : Controller
    {
        [HttpGet]
        public IActionResult Get([FromQuery] Filter filter)
        {
            IEnumerable<object> result = _db.FetchAssessments(filter);
            return new HttpOkObjectResult(result);
        }

        [HttpGet("{id}", Name = "assessment")]
        public IActionResult Get(int id)
        {
            var result = _db.FetchAssessment(id);
            if (result == null)
            {
                return new HttpNotFoundResult();
            }

            return new HttpOkObjectResult(result);
        }

        [HttpPatch("{id}")]
        public IActionResult Patch([FromBody] IEnumerable<Patch> patches, int id)
        {
            List<Error> errors = new List<Error>();

            if (!ModelState.IsValid)
            {
                if (_db.GetModelStateErrors(ModelState))
                {
                    return new BadRequestObjectResult(_db.FatalError);
                }
                else
                {
                    return new UnprocessableEntityObjectResult(_db.Errors);
                }
            }

            if (patches == null)
            {
                return new UnprocessableEntityObjectResult(new Error(1100));
            }

            if (!_db.AssessmentExists(id))
            {
                return new HttpNotFoundResult();
            }

            _db.PatchAssessment(patches, id);

            if (_db.Errors.Any() || _val.Errors.Any())
            {
                errors.AddRange(_db.Errors);
                errors.AddRange(_val.Errors);
                _val.ClearErrors();
                _db.ClearErrors();
                return new UnprocessableEntityObjectResult(errors);
            }

            var result = _db.FetchAssessment(id);
            return new HttpOkObjectResult(result);
        }


        [HttpPost("{subject}/{cohort}/{name}")]
        public IActionResult Post([FromBody] AssessmentPost assessment, string subject, string cohort, string name)
        {
            subject = Misc.CleanParam(subject);
            name = Misc.CleanParam(name);

            if (!ModelState.IsValid)
            {
                if (_db.GetModelStateErrors(ModelState))
                {
                    return new BadRequestObjectResult(_db.FatalError);
                }
                else
                {
                    return new UnprocessableEntityObjectResult(_db.Errors);
                }
            }

            if (assessment == null)
            {
                return new UnprocessableEntityObjectResult(new Error(1100));
            }

            dynamic examResult = _db.FetchExam(subject, name, cohort);
            if(examResult == null)
            {
                return new HttpNotFoundResult();
            }

            var id = _db.AddAssessment(assessment, subject, name, cohort, examResult);

            if (_db.Errors.Any())
            {
                return new UnprocessableEntityObjectResult(_db.Errors);
            }

            var result = _db.FetchAssessment(id);
            string location = Url.RouteUrl("assessment", new { id = id }, Request.Scheme);
            return new CreatedResult(location, result);
        }

        private readonly Database _db = new Database();
        private readonly Validate _val = new Validate();
    }
}