﻿using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;
using Service.Core.Client.Models;
using Service.Education.Helpers;
using Service.EducationRetry.Grpc;
using Service.EducationRetry.Grpc.Models;
using Service.EducationRetryApi.Models;
using Service.Grpc;
using Service.Web;

namespace Service.EducationRetryApi.Controllers
{
	[Authorize]
	[ApiController]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	[SwaggerResponse(HttpStatusCode.Unauthorized, null, Description = "Unauthorized")]
	[OpenApiTag("Retry", Description = "Task retry")]
	[Route("/api/v1/retry")]
	public class TaskRetryController : ControllerBase
	{
		private readonly IGrpcServiceProxy<IEducationRetryService> _educationRetryService;

		public TaskRetryController(IGrpcServiceProxy<IEducationRetryService> educationRetryService) => _educationRetryService = educationRetryService;

		[HttpPost("count")]
		[SwaggerResponse(HttpStatusCode.OK, typeof (DataResponse<int>), Description = "Ok")]
		public async ValueTask<IActionResult> GetRetryCountAsync()
		{
			Guid? userId = GetUserId();
			if (userId == null)
				return StatusResponse.Error(ResponseCode.UserNotFound);

			RetryCountGrpcResponse response = await _educationRetryService.Service.GetRetryCountAsync(new GetRetryCountGrpcRequest
			{
				UserId = userId
			});

			return DataResponse<int>.Ok(response.Count);
		}

		[HttpPost("use-bydate")]
		[SwaggerResponse(HttpStatusCode.OK, typeof (DataResponse<int>), Description = "Ok")]
		public async ValueTask<IActionResult> UseRetryByDateAsync([FromBody] UseRetryRequest request) =>
			await Process(request, userId => _educationRetryService.TryCall(service => service.DecreaseRetryDateAsync(new DecreaseRetryDateGrpcRequest
			{
				UserId = userId,
				Tutorial = request.Tutorial,
				Unit = request.Unit,
				Task = request.Task
			})));

		[HttpPost("use-bycount")]
		[SwaggerResponse(HttpStatusCode.OK, typeof (DataResponse<int>), Description = "Ok")]
		public async ValueTask<IActionResult> UseRetryByCountAsync([FromBody] UseRetryRequest request) =>
			await Process(request, userId => _educationRetryService.TryCall(service => service.DecreaseRetryCountAsync(new DecreaseRetryCountGrpcRequest
			{
				UserId = userId,
				Tutorial = request.Tutorial,
				Unit = request.Unit,
				Task = request.Task
			})));

		private async ValueTask<IActionResult> Process(UseRetryRequest request, Func<Guid?, ValueTask<CommonGrpcResponse>> grpcRequestFunc)
		{
			if (EducationHelper.GetTask(request.Tutorial, request.Unit, request.Task) == null)
				return StatusResponse.Error(ResponseCode.NotValidEducationRequestData);

			Guid? userId = GetUserId();
			if (userId == null)
				return StatusResponse.Error(ResponseCode.UserNotFound);

			CommonGrpcResponse response = await grpcRequestFunc.Invoke(userId);

			return StatusResponse.Result(response);
		}

		private Guid? GetUserId() => Guid.TryParse(User.Identity?.Name, out Guid uid) ? (Guid?)uid : null;
	}
}