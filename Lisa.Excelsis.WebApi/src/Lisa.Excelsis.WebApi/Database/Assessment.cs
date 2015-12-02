﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Lisa.Excelsis.WebApi
{
    partial class Database
    {
        public object FetchAssessment(object id)
        {
            var query = @"SELECT Assessments.Id as [@], Assessments.Id, StudentNumber as Student_@Number, StudentName as Student_Name, StudentNumber as Student_Number, Assessed,
                                 Exams.Id as Exam_@Id, Exams.Name as Exam_Name, Exams.Cohort as Exam_Cohort, Exams.Crebo as Exam_Crebo, Exams.Subject as Exam_Subject,
                                 Assessors.Id as #Assessors_@Id, Assessors.UserName as #Assessors_UserName,
                                 Categories.Id as #Categories_@Id, Categories.Id as #Categories_Id, Categories.[Order] as #Categories_Order, Categories.Name as #Categories_Name,
                                 Observations.Id as #Categories_#Observations_@Id, Observations.Id as #Categories_#Observations_Id, Observations.Result as #Categories_#Observations_Result,
                                 Marks.Id as #Categories_#Observations_#Marks_@Id, Marks.Name as #Categories_#Observations_#Marks_Name,
                                 Criteria.Id as #Categories_#Observations_Criterion_@Id, Criteria.Title as #Categories_#Observations_Criterion_Title, Criteria.Description as #Categories_#Observations_Criterion_Description, Criteria.[Order] as #Categories_#Observations_Criterion_Order, Criteria.Value as #Categories_#Observations_Criterion_Value
                          FROM Assessments
                          LEFT JOIN Exams ON Exams.Id = Assessments.Exam_Id
                          LEFT JOIN AssessmentsAssessors ON AssessmentsAssessors.Assessment_Id = Assessments.Id
                          LEFT JOIN Assessors ON Assessors.Id = AssessmentsAssessors.Assessor_Id
                          LEFT JOIN Observations ON Observations.Assessment_Id = Assessments.Id
                          LEFT JOIN Marks ON Marks.Observation_Id = Observations.Id
                          LEFT JOIN Criteria ON Criteria.Id = Observations.Criterion_Id
                          LEFT JOIN Categories ON Categories.Id = Criteria.CategoryId
                          WHERE Assessments.Id = @Id";
            var parameters = new {
                Id = id
            };

            dynamic result = _gateway.SelectSingle(query, parameters);
           
            foreach(dynamic category in result.Categories)
            {
                foreach(dynamic observation in category.Observations)
                {
                    List<string> marks = new List<string>();
                    foreach (dynamic mark in observation.Marks)
                    {
                        marks.Add(mark.Name);
                    }
                    observation.Marks = marks.GroupBy(m => m).Select(g => g.First()).ToArray();
                }
            }
            return result;
        }

        public IEnumerable<object> FetchAssessments(Filter filter)
        {
            List<string> queryList = new List<string>();

            var query = @"SELECT Assessments.Id as [@], Assessments.Id, StudentNumber as Student_@, StudentName as Student_Name, StudentNumber as Student_Number, Assessed,
                                 Exams.Id as Exam_@ID, Exams.Name as Exam_Name, Exams.Cohort as Exam_Cohort, Exams.Crebo as Exam_Crebo, Exams.Subject as Exam_Subject,
                                 Assessors.Id as #Assessors_@Id, Assessors.UserName as #Assessors_UserName
                          FROM Assessments
                          LEFT JOIN Exams ON Exams.Id = Assessments.Exam_Id
                          LEFT JOIN AssessmentsAssessors ON AssessmentsAssessors.Assessment_Id = Assessments.Id
                          LEFT JOIN Assessors ON Assessors.Id = AssessmentsAssessors.Assessor_Id";

            if (filter.Assessor != null)
            {
                queryList.Add( @" Assessments.Id IN(
                                      SELECT Assessments.Id
                                      FROM Assessments
                                      LEFT JOIN Exams ON Exams.Id = Assessments.Exam_Id
                                      LEFT JOIN AssessmentsAssessors ON AssessmentsAssessors.Assessment_Id = Assessments.Id
                                      LEFT JOIN Assessors ON Assessors.Id = AssessmentsAssessors.Assessor_Id
                                      WHERE Assessors.UserName = @Assessor
                                  )");
            }

            if (filter.StudentNumber != null)
            {
                queryList.Add(" Assessments.StudentNumber = @StudentNumber");
            }

            var parameters = new
            {
                Assessor = filter.Assessor ?? string.Empty,
                StudentNumber = filter.StudentNumber ?? string.Empty
            };

            query += (queryList.Count > 0) ? " WHERE " + string.Join(" AND ", queryList) : string.Join(" AND ", queryList);
            return _gateway.SelectMany(query, parameters);
        }

        public object AddAssessment(AssessmentPost assessment, string subject, string name, string cohort, dynamic examResult)
        {
            _errors = new List<Error>();
            if (assessment.Student != null)
            {
                var regexName = new Regex(@"^\s*(\w+\s)*\w+\s*$");
                if (assessment.Student.Name != null && !regexName.IsMatch(assessment.Student.Name))
                {
                    _errors.Add(new Error(1101, string.Format("The student name '{0}' may only contain characters", assessment.Student.Name), new
                    {
                        StudentName = assessment.Student.Name
                    }));
                }

                var regexNumber = new Regex(@"^\d{8}$");
                if (assessment.Student.Number != null && !regexNumber.IsMatch(assessment.Student.Number))
                {
                    _errors.Add(new Error(1102, string.Format("The student number '{0}' doesn't meet the requirements of 8 digits", assessment.Student.Number), new
                    {
                        StudentNumber = assessment.Student.Number
                    }));
                }
            }
            else
            {
                assessment.Student = new Student();
            }

            object assessorResult = SelectAssessors(assessment);

            if (_errors.Count() == 0)
            {
                object assessmentResult = InsertAssessment(assessment, examResult);
                InsertAssessmentAssessors(assessment, assessmentResult, assessorResult);
                AddObservations(assessmentResult, examResult);

                return (_errors.Count() > 0) ? null : assessmentResult;
            }

            return null;
        }

        public void PatchAssessment(IEnumerable<Patch> patches, int id)
        {
            _errors = new List<Error>();
            foreach (Patch patch in patches)
            {
                var fieldString = patch.Field.Split('/');
                if (fieldString.ElementAt(0).ToLower() == "observations")
                {
                    int observationId = Convert.ToInt32(fieldString.ElementAt(1));
                    PatchObservation(patch.Action, id, observationId, fieldString.ElementAt(2), patch.Value);
                }

                switch (patch.Action)
                {
                    case "add":
                        break;
                    case "replace":
                        break;
                    case "remove":
                        break;
                }
            }
        }

        private object SelectAssessors(AssessmentPost assessment)
        {
            var assessors = assessment.Assessors.Select(assessor => "'" + assessor + "'");

            var query = @"SELECT Id, UserName
                          FROM Assessors
                          WHERE UserName IN ( " + string.Join(",", assessors) + " ) ";
            dynamic result = _gateway.SelectMany(query);

           
            if (result.Count != assessment.Assessors.Count())
            {
                foreach(var assessor in assessment.Assessors)
                {
                    if (result.Count == 0 || (result.Count > 0 && !((IEnumerable<dynamic>)result).Any(a => a.UserName == assessor)))
                    {
                        _errors.Add(new Error(1108, string.Format("The assessor with username '{0}' is not found.", assessor), new
                        {
                            Assessor = assessor
                        }));
                    }
                }
            }

            return result;
        }

        private object InsertAssessment(AssessmentPost assessment, dynamic examResult)
        {
            var query = @"INSERT INTO Assessments (StudentName, StudentNumber, Assessed, Exam_Id)
                          VALUES (@StudentName, @StudentNumber, @Assessed, @ExamId);";

            var parameters = new
            {
                StudentName = assessment.Student.Name ?? string.Empty,
                StudentNumber = assessment.Student.Number ?? string.Empty,
                Assessed = assessment.Assessed,
                ExamId = examResult.Id
            };

            return _gateway.Insert(query, parameters);
        }

       

        private void InsertAssessmentAssessors(AssessmentPost assessment, dynamic assessmentResult, dynamic assessorResult)
        {
            var assessorAssessments = ((IEnumerable<dynamic>)assessorResult).Select(assessor => "(" + assessmentResult + ", " + assessor.Id + ")");

            var query = @"INSERT INTO AssessmentsAssessors (Assessment_Id, Assessor_Id) VALUES ";
            query += string.Join(",", assessorAssessments);
            _gateway.Insert(query, null);
        }
    }
}