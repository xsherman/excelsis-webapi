﻿using System.Collections.Generic;

namespace Lisa.Excelsis.WebApi
{
    partial class Database
    {
        public object FetchCategory(int id, int examId)
        {
            var query = @"SELECT Id, [Order], Name
                          FROM Categories 
                          WHERE Categories.Id = @Id AND Categories.ExamId = @ExamId";

            var parameters = new
            {
                Id = id,
                ExamId = examId
            };

            return _gateway.SelectSingle(query, parameters);
        }

        public bool CategoryExists(int examId, int id)
        {
            _errors = new List<Error>();

            var query = @"SELECT COUNT(*) as count FROM Categories
                          WHERE ExamId = @ExamId AND Id = @Id";
            dynamic result = _gateway.SelectSingle(query, new { ExamId = examId, Id = id });

            return (result.count > 0);
        }
    }
}
