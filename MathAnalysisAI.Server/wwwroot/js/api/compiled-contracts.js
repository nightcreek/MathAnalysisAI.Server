window.CompiledApiContracts = {
  "version": "1.0.0",
  "modules": {
    "admin": {
      "version": "1.0.0",
      "endpoints": {
        "dashboard": {
          "endpoint": "/api/admin/dashboard",
          "method": "GET",
          "description": "Returns aggregate admin dashboard counters and daily stats.",
          "requestSchema": {
            "type": "object",
            "properties": {}
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "totalUsers",
              "totalAnalyses",
              "totalQuestions",
              "dailyStats"
            ],
            "properties": {
              "totalUsers": {
                "type": "number"
              },
              "totalAnalyses": {
                "type": "number"
              },
              "totalQuestions": {
                "type": "number"
              },
              "totalOcrRecords": {
                "type": "number"
              },
              "dailyStats": {
                "type": "array",
                "items": {
                  "type": "object"
                }
              }
            }
          }
        },
        "users": {
          "endpoint": "/api/admin/users",
          "method": "GET",
          "description": "Lists users for admin management.",
          "requestSchema": {
            "type": "object",
            "properties": {
              "search": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "role": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "page": {
                "type": [
                  "number",
                  "null"
                ]
              },
              "pageSize": {
                "type": [
                  "number",
                  "null"
                ]
              }
            }
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "items",
              "totalCount",
              "page",
              "pageSize"
            ],
            "properties": {
              "items": {
                "type": "array",
                "items": {
                  "type": "object"
                }
              },
              "totalCount": {
                "type": "number"
              },
              "page": {
                "type": "number"
              },
              "pageSize": {
                "type": "number"
              }
            }
          }
        },
        "updateUserRole": {
          "endpointTemplate": "/api/admin/users/{userId}/role",
          "method": "PUT",
          "description": "Updates a user's role.",
          "requestSchema": {
            "type": "object",
            "required": [
              "role"
            ],
            "properties": {
              "role": {
                "type": "string"
              }
            }
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "message"
            ],
            "properties": {
              "message": {
                "type": "string"
              }
            }
          }
        },
        "teachers": {
          "endpoint": "/api/admin/teachers",
          "method": "GET",
          "description": "Lists teachers for admin tools.",
          "requestSchema": {
            "type": "object",
            "properties": {}
          },
          "responseSchema": {
            "type": "array",
            "items": {
              "type": "object",
              "required": [
                "id",
                "username",
                "role",
                "studentCount"
              ],
              "properties": {
                "id": {
                  "type": "number"
                },
                "username": {
                  "type": "string"
                },
                "realName": {
                  "type": [
                    "string",
                    "null"
                  ]
                },
                "role": {
                  "type": "string"
                },
                "studentCount": {
                  "type": "number"
                }
              }
            }
          }
        },
        "createTeacher": {
          "endpoint": "/api/admin/teachers",
          "method": "POST",
          "description": "Creates a teacher account.",
          "requestSchema": {
            "type": "object",
            "required": [
              "username",
              "password"
            ],
            "properties": {
              "username": {
                "type": "string"
              },
              "password": {
                "type": "string"
              },
              "realName": {
                "type": [
                  "string",
                  "null"
                ]
              }
            }
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "userId",
              "username",
              "message"
            ],
            "properties": {
              "userId": {
                "type": "number"
              },
              "username": {
                "type": "string"
              },
              "message": {
                "type": "string"
              }
            }
          }
        },
        "importStudents": {
          "endpoint": "/api/admin/import-students",
          "method": "POST",
          "description": "Imports students for a teacher.",
          "requestSchema": {
            "type": "object",
            "required": [
              "teacherId",
              "students"
            ],
            "properties": {
              "teacherId": {
                "type": "number"
              },
              "students": {
                "type": "array",
                "items": {
                  "type": "object",
                  "properties": {
                    "realName": {
                      "type": [
                        "string",
                        "null"
                      ]
                    },
                    "studentNumber": {
                      "type": [
                        "string",
                        "null"
                      ]
                    }
                  }
                }
              }
            }
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "created",
              "skipped",
              "errors",
              "total",
              "teacherId"
            ],
            "properties": {
              "created": {
                "type": "number"
              },
              "skipped": {
                "type": "number"
              },
              "errors": {
                "type": "array",
                "items": {
                  "type": "string"
                }
              },
              "total": {
                "type": "number"
              },
              "teacherId": {
                "type": "number"
              }
            }
          }
        },
        "teacherStudents": {
          "endpointTemplate": "/api/admin/teachers/{teacherId}/students",
          "method": "GET",
          "description": "Returns students assigned to a teacher.",
          "requestSchema": {
            "type": "object",
            "properties": {}
          },
          "responseSchema": {
            "type": "array",
            "items": {
              "type": "object",
              "required": [
                "id",
                "username"
              ],
              "properties": {
                "id": {
                  "type": "number"
                },
                "username": {
                  "type": "string"
                },
                "realName": {
                  "type": [
                    "string",
                    "null"
                  ]
                },
                "studentNumber": {
                  "type": [
                    "string",
                    "null"
                  ]
                },
                "className": {
                  "type": [
                    "string",
                    "null"
                  ]
                }
              }
            }
          }
        }
      }
    },
    "analysis": {
      "version": "1.0.0",
      "endpoints": {
        "run": {
          "endpoint": "/api/learning-analysis/analyze",
          "method": "POST",
          "description": "Runs the analysis pipeline and returns the structured analysis response.",
          "requestSchema": {
            "type": "object",
            "required": [
              "courseId",
              "problemText",
              "analysisMode"
            ],
            "properties": {
              "courseId": {
                "type": "number"
              },
              "chapterId": {
                "type": [
                  "number",
                  "null"
                ]
              },
              "problemText": {
                "type": "string"
              },
              "studentSolutionText": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "analysisMode": {
                "type": "string"
              },
              "userId": {
                "type": [
                  "number",
                  "null"
                ]
              },
              "ocrRecordId": {
                "type": [
                  "number",
                  "null"
                ]
              },
              "structuredProblemId": {
                "type": [
                  "number",
                  "null"
                ]
              },
              "formulas": {
                "type": [
                  "array",
                  "null"
                ],
                "items": {
                  "type": "object",
                  "required": [
                    "latex"
                  ],
                  "properties": {
                    "latex": {
                      "type": "string"
                    },
                    "context": {
                      "type": [
                        "string",
                        "null"
                      ]
                    }
                  }
                }
              }
            }
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "course",
              "problemType",
              "difficulty",
              "knowledgePoints",
              "solutionOverview",
              "standardSolution",
              "studentSolutionReview",
              "mistakeTags",
              "reviewSuggestions",
              "visualization"
            ],
            "properties": {
              "analysisResultId": {
                "type": [
                  "number",
                  "null"
                ]
              },
              "problemId": {
                "type": [
                  "number",
                  "null"
                ]
              },
              "studentSolutionId": {
                "type": [
                  "number",
                  "null"
                ]
              },
              "course": {
                "type": "string"
              },
              "chapter": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "problemType": {
                "type": "string"
              },
              "difficulty": {
                "type": "string"
              },
              "knowledgePoints": {
                "type": "array",
                "items": {
                  "type": "string"
                }
              },
              "solutionOverview": {
                "type": "string"
              },
              "standardSolution": {
                "type": "array",
                "items": {
                  "type": "object"
                }
              },
              "studentSolutionReview": {
                "type": "object"
              },
              "mistakeTags": {
                "type": "array",
                "items": {
                  "type": "string"
                }
              },
              "reviewSuggestions": {
                "type": "array",
                "items": {
                  "type": "string"
                }
              },
              "visualization": {
                "type": "object"
              }
            }
          }
        },
        "stream": {
          "endpoint": "/api/learning-analysis/analyze/stream",
          "method": "POST",
          "description": "Starts the stream analysis endpoint and returns SSE chunks ending with [DONE].",
          "requestSchema": {
            "type": "object",
            "required": [
              "courseId",
              "problemText",
              "analysisMode"
            ],
            "properties": {
              "courseId": {
                "type": "number"
              },
              "chapterId": {
                "type": [
                  "number",
                  "null"
                ]
              },
              "problemText": {
                "type": "string"
              },
              "studentSolutionText": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "analysisMode": {
                "type": "string"
              }
            }
          },
          "responseSchema": {
            "type": "object",
            "streaming": true,
            "chunkType": "json-string"
          }
        }
      }
    },
    "auth": {
      "version": "1.0.0",
      "endpoints": {
        "info": {
          "endpoint": "/api/auth/info",
          "method": "GET",
          "description": "Returns current auth mode and optional OIDC metadata.",
          "requestSchema": {
            "type": "object",
            "properties": {}
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "mode"
            ],
            "properties": {
              "mode": {
                "type": "string"
              },
              "oidc": {
                "type": [
                  "object",
                  "null"
                ]
              }
            }
          }
        },
        "me": {
          "endpoint": "/api/auth/me",
          "method": "GET",
          "description": "Returns the authenticated user context.",
          "requestSchema": {
            "type": "object",
            "properties": {}
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "userId",
              "username",
              "role"
            ],
            "properties": {
              "userId": {
                "type": "number"
              },
              "username": {
                "type": "string"
              },
              "realName": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "studentNumber": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "role": {
                "type": "string"
              },
              "impersonatedRole": {
                "type": [
                  "string",
                  "null"
                ]
              }
            }
          }
        },
        "login": {
          "endpoint": "/api/auth/login",
          "method": "POST",
          "description": "Logs in with username and optional password depending on auth mode.",
          "requestSchema": {
            "type": "object",
            "required": [
              "username"
            ],
            "properties": {
              "username": {
                "type": "string"
              },
              "password": {
                "type": [
                  "string",
                  "null"
                ]
              }
            }
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "accessToken",
              "expiresAtUtc",
              "user"
            ],
            "properties": {
              "accessToken": {
                "type": "string"
              },
              "expiresAtUtc": {
                "type": "string"
              },
              "user": {
                "type": "object",
                "required": [
                  "userId",
                  "username",
                  "role"
                ],
                "properties": {
                  "userId": {
                    "type": "number"
                  },
                  "username": {
                    "type": "string"
                  },
                  "role": {
                    "type": "string"
                  }
                }
              }
            }
          }
        },
        "register": {
          "endpoint": "/api/auth/register",
          "method": "POST",
          "description": "Registers a user and returns the authenticated token payload.",
          "requestSchema": {
            "type": "object",
            "required": [
              "username",
              "password"
            ],
            "properties": {
              "username": {
                "type": "string"
              },
              "password": {
                "type": "string"
              },
              "realName": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "studentNumber": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "schoolName": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "departmentName": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "className": {
                "type": [
                  "string",
                  "null"
                ]
              }
            }
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "accessToken",
              "expiresAtUtc",
              "user"
            ],
            "properties": {
              "accessToken": {
                "type": "string"
              },
              "expiresAtUtc": {
                "type": "string"
              },
              "user": {
                "type": "object"
              }
            }
          }
        },
        "impersonate": {
          "endpoint": "/api/auth/impersonate",
          "method": "POST",
          "description": "Sets or clears admin frontend impersonation role.",
          "requestSchema": {
            "type": "object",
            "required": [
              "role"
            ],
            "properties": {
              "role": {
                "type": [
                  "string",
                  "null"
                ]
              }
            }
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "message"
            ],
            "properties": {
              "role": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "message": {
                "type": "string"
              }
            }
          }
        },
        "changePassword": {
          "endpoint": "/api/auth/password",
          "method": "PUT",
          "description": "Changes the current user password.",
          "requestSchema": {
            "type": "object",
            "required": [
              "newPassword"
            ],
            "properties": {
              "newPassword": {
                "type": "string"
              }
            }
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "success"
            ],
            "properties": {
              "success": {
                "type": "boolean"
              }
            }
          }
        },
        "logout": {
          "endpoint": "/api/auth/logout",
          "method": "POST",
          "description": "Frontend logout acknowledgement endpoint.",
          "requestSchema": {
            "type": "object",
            "properties": {}
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "success"
            ],
            "properties": {
              "success": {
                "type": "boolean"
              }
            }
          }
        },
        "joinClass": {
          "endpoint": "/api/auth/join-class",
          "method": "POST",
          "description": "Joins a class and returns the updated current user payload.",
          "requestSchema": {
            "type": "object",
            "properties": {
              "teacherId": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "teacherUsername": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "realName": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "studentNumber": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "schoolName": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "className": {
                "type": [
                  "string",
                  "null"
                ]
              }
            }
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "userId",
              "username",
              "role"
            ],
            "properties": {
              "userId": {
                "type": "number"
              },
              "username": {
                "type": "string"
              },
              "role": {
                "type": "string"
              }
            }
          }
        }
      }
    },
    "question": {
      "version": "1.0.0",
      "endpoints": {
        "list": {
          "endpoint": "/api/resources",
          "method": "GET",
          "description": "Frontend compatibility adapter for the removed /api/questions endpoint. Source backend data comes from /api/resources and is transformed into question-like cards.",
          "adapterSource": "resources.list",
          "requestSchema": {
            "type": "object",
            "properties": {
              "search": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "difficulty": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "questionType": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "take": {
                "type": [
                  "number",
                  "null"
                ]
              }
            }
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "items"
            ],
            "properties": {
              "items": {
                "type": "array",
                "items": {
                  "type": "object",
                  "required": [
                    "id",
                    "title",
                    "content",
                    "difficulty",
                    "questionType"
                  ],
                  "properties": {
                    "id": {
                      "type": "number"
                    },
                    "title": {
                      "type": "string"
                    },
                    "content": {
                      "type": "string"
                    },
                    "difficulty": {
                      "type": "string"
                    },
                    "questionType": {
                      "type": "string"
                    },
                    "link": {
                      "type": [
                        "string",
                        "null"
                      ]
                    }
                  }
                }
              }
            }
          }
        }
      }
    },
    "resources": {
      "version": "1.0.0",
      "endpoints": {
        "list": {
          "endpoint": "/api/resources",
          "method": "GET",
          "description": "Lists public network resources, optionally filtered by course.",
          "requestSchema": {
            "type": "object",
            "properties": {
              "courseId": {
                "type": [
                  "number",
                  "null"
                ]
              }
            }
          },
          "responseSchema": {
            "type": "array",
            "items": {
              "type": "object",
              "required": [
                "id",
                "courseId",
                "category",
                "title",
                "sortOrder",
                "isEnabled",
                "createdAt",
                "updatedAt"
              ],
              "properties": {
                "id": {
                  "type": "number"
                },
                "courseId": {
                  "type": "number"
                },
                "category": {
                  "type": "string"
                },
                "title": {
                  "type": "string"
                },
                "description": {
                  "type": [
                    "string",
                    "null"
                  ]
                },
                "link": {
                  "type": [
                    "string",
                    "null"
                  ]
                },
                "sortOrder": {
                  "type": "number"
                },
                "isEnabled": {
                  "type": "boolean"
                },
                "createdAt": {
                  "type": "string"
                },
                "updatedAt": {
                  "type": "string"
                }
              }
            }
          }
        },
        "create": {
          "endpoint": "/api/resources",
          "method": "POST",
          "description": "Creates a network resource.",
          "requestSchema": {
            "type": "object",
            "required": [
              "courseId",
              "category",
              "title"
            ],
            "properties": {
              "courseId": {
                "type": "number"
              },
              "category": {
                "type": "string"
              },
              "title": {
                "type": "string"
              },
              "description": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "link": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "sortOrder": {
                "type": [
                  "number",
                  "null"
                ]
              }
            }
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "id",
              "courseId",
              "category",
              "title"
            ],
            "properties": {
              "id": {
                "type": "number"
              },
              "courseId": {
                "type": "number"
              },
              "category": {
                "type": "string"
              },
              "title": {
                "type": "string"
              }
            }
          }
        },
        "update": {
          "endpointTemplate": "/api/resources/{resourceId}",
          "method": "PUT",
          "description": "Updates a network resource.",
          "requestSchema": {
            "type": "object",
            "properties": {
              "category": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "title": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "description": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "link": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "sortOrder": {
                "type": [
                  "number",
                  "null"
                ]
              },
              "isEnabled": {
                "type": [
                  "boolean",
                  "null"
                ]
              }
            }
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "id",
              "courseId",
              "category",
              "title"
            ],
            "properties": {
              "id": {
                "type": "number"
              },
              "courseId": {
                "type": "number"
              },
              "category": {
                "type": "string"
              },
              "title": {
                "type": "string"
              }
            }
          }
        },
        "delete": {
          "endpointTemplate": "/api/resources/{resourceId}",
          "method": "DELETE",
          "description": "Deletes a network resource.",
          "requestSchema": {
            "type": "object",
            "properties": {}
          },
          "responseSchema": {
            "type": [
              "object",
              "null"
            ]
          }
        },
        "getById": {
          "endpointTemplate": "/api/resources/{resourceId}",
          "method": "GET",
          "description": "Gets a single network resource.",
          "requestSchema": {
            "type": "object",
            "properties": {}
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "id",
              "courseId",
              "category",
              "title"
            ],
            "properties": {
              "id": {
                "type": "number"
              },
              "courseId": {
                "type": "number"
              },
              "category": {
                "type": "string"
              },
              "title": {
                "type": "string"
              }
            }
          }
        }
      }
    },
    "support": {
      "version": "1.0.0",
      "endpoints": {
        "coursesList": {
          "endpoint": "/api/courses",
          "method": "GET",
          "description": "Lists available courses.",
          "requestSchema": {
            "type": "object",
            "properties": {}
          },
          "responseSchema": {
            "type": "array",
            "items": {
              "type": "object"
            }
          }
        },
        "courseChapters": {
          "endpointTemplate": "/api/courses/{courseId}/chapters",
          "method": "GET",
          "description": "Lists chapters for a course.",
          "requestSchema": {
            "type": "object",
            "properties": {}
          },
          "responseSchema": {
            "type": "array",
            "items": {
              "type": "object"
            }
          }
        },
        "courseMaterialsList": {
          "endpoint": "/api/course-materials",
          "method": "GET",
          "description": "Lists course materials.",
          "requestSchema": {
            "type": "object",
            "required": [
              "courseId"
            ],
            "properties": {
              "courseId": {
                "type": "number"
              },
              "chapterId": {
                "type": [
                  "number",
                  "null"
                ]
              },
              "parseStatus": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "take": {
                "type": [
                  "number",
                  "null"
                ]
              }
            }
          },
          "responseSchema": {
            "type": "array",
            "items": {
              "type": "object",
              "required": [
                "materialId",
                "courseId",
                "title",
                "materialKind",
                "parseStatus",
                "chunkCount",
                "uploadedAt"
              ],
              "properties": {
                "materialId": {
                  "type": "number"
                },
                "courseId": {
                  "type": "number"
                },
                "title": {
                  "type": "string"
                },
                "materialKind": {
                  "type": "string"
                },
                "parseStatus": {
                  "type": "string"
                },
                "chunkCount": {
                  "type": "number"
                },
                "uploadedAt": {
                  "type": "string"
                }
              }
            }
          }
        },
        "courseMaterialsSearch": {
          "endpoint": "/api/course-materials/search",
          "method": "GET",
          "description": "Searches course material knowledge chunks.",
          "requestSchema": {
            "type": "object",
            "required": [
              "courseId",
              "q"
            ],
            "properties": {
              "courseId": {
                "type": "number"
              },
              "chapterId": {
                "type": [
                  "number",
                  "null"
                ]
              },
              "q": {
                "type": "string"
              },
              "studentSolutionText": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "topK": {
                "type": [
                  "number",
                  "null"
                ]
              }
            }
          },
          "responseSchema": {
            "type": "array",
            "items": {
              "type": "object"
            }
          }
        },
        "courseMaterialsUpload": {
          "endpoint": "/api/course-materials/upload",
          "method": "POST",
          "description": "Uploads a course material PDF.",
          "requestSchema": {
            "type": "object",
            "transport": "multipart/form-data",
            "required": [
              "courseId",
              "file"
            ],
            "properties": {
              "courseId": {
                "type": "number"
              },
              "chapterId": {
                "type": [
                  "number",
                  "null"
                ]
              },
              "title": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "materialKind": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "visibility": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "file": {
                "type": "file"
              }
            }
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "materialId",
              "title",
              "originalFileName",
              "parseStatus",
              "chunkCount"
            ],
            "properties": {
              "materialId": {
                "type": "number"
              },
              "title": {
                "type": "string"
              },
              "originalFileName": {
                "type": "string"
              },
              "parseStatus": {
                "type": "string"
              },
              "chunkCount": {
                "type": "number"
              }
            }
          }
        },
        "leaderboardPublic": {
          "endpoint": "/api/leaderboard/public",
          "method": "GET",
          "description": "Returns the public leaderboard.",
          "requestSchema": {
            "type": "object",
            "properties": {
              "courseId": {
                "type": [
                  "number",
                  "null"
                ]
              },
              "take": {
                "type": [
                  "number",
                  "null"
                ]
              }
            }
          },
          "responseSchema": {
            "type": "array",
            "items": {
              "type": "object",
              "required": [
                "rank",
                "username",
                "attemptCount",
                "correctCount",
                "wrongCount",
                "accuracyRate",
                "rankingScore"
              ],
              "properties": {
                "rank": {
                  "type": "number"
                },
                "username": {
                  "type": "string"
                },
                "attemptCount": {
                  "type": "number"
                },
                "correctCount": {
                  "type": "number"
                },
                "wrongCount": {
                  "type": "number"
                },
                "accuracyRate": {
                  "type": [
                    "number",
                    "string"
                  ]
                },
                "rankingScore": {
                  "type": [
                    "number",
                    "string"
                  ]
                }
              }
            }
          }
        },
        "statsPersonal": {
          "endpoint": "/api/stats/personal",
          "method": "GET",
          "description": "Returns personal stats summary, course progress, and knowledge mastery.",
          "requestSchema": {
            "type": "object",
            "properties": {
              "courseId": {
                "type": [
                  "number",
                  "null"
                ]
              }
            }
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "summary",
              "courseProgress",
              "knowledgeMastery"
            ],
            "properties": {
              "summary": {
                "type": "object"
              },
              "courseProgress": {
                "type": "array",
                "items": {
                  "type": "object"
                }
              },
              "knowledgeMastery": {
                "type": "array",
                "items": {
                  "type": "object"
                }
              }
            }
          }
        },
        "statsKnowledgeMastery": {
          "endpoint": "/api/stats/knowledge-mastery",
          "method": "GET",
          "description": "Returns knowledge mastery rows for current user.",
          "requestSchema": {
            "type": "object",
            "properties": {
              "courseId": {
                "type": [
                  "number",
                  "null"
                ]
              }
            }
          },
          "responseSchema": {
            "type": "array",
            "items": {
              "type": "object"
            }
          }
        },
        "learningPath": {
          "endpoint": "/api/learningpath",
          "method": "GET",
          "description": "Returns learning path recommendations.",
          "requestSchema": {
            "type": "object",
            "properties": {
              "courseId": {
                "type": [
                  "number",
                  "null"
                ]
              }
            }
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "courseId",
              "courseName",
              "recommendedOrder",
              "weakPoints"
            ],
            "properties": {
              "courseId": {
                "type": "number"
              },
              "courseName": {
                "type": "string"
              },
              "recommendedOrder": {
                "type": "array"
              },
              "weakPoints": {
                "type": "array"
              }
            }
          }
        },
        "learningPathWeakPoints": {
          "endpoint": "/api/learningpath/weak-points",
          "method": "GET",
          "description": "Returns weak points list for learning path module.",
          "requestSchema": {
            "type": "object",
            "properties": {
              "courseId": {
                "type": [
                  "number",
                  "null"
                ]
              }
            }
          },
          "responseSchema": {
            "type": "array",
            "items": {
              "type": "object"
            }
          }
        },
        "health": {
          "endpoint": "/api/health",
          "method": "GET",
          "description": "Returns service health status.",
          "requestSchema": {
            "type": "object",
            "properties": {}
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "status",
              "service",
              "timestampUtc"
            ],
            "properties": {
              "status": {
                "type": "string"
              },
              "service": {
                "type": "string"
              },
              "timestampUtc": {
                "type": "string"
              }
            }
          }
        },
        "photoOcr": {
          "endpoint": "/api/photo-solutions/ocr",
          "method": "POST",
          "description": "Uploads an image and returns OCR extraction result.",
          "requestSchema": {
            "type": "object",
            "transport": "multipart/form-data",
            "required": [
              "courseId",
              "file"
            ],
            "properties": {
              "courseId": {
                "type": "number"
              },
              "chapterId": {
                "type": [
                  "number",
                  "null"
                ]
              },
              "userHint": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "file": {
                "type": "file"
              }
            }
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "isSuccess",
              "problemText",
              "studentSolutionText",
              "detectedSections",
              "formulas",
              "warnings",
              "reviewReasons",
              "status",
              "needsManualReview",
              "isConfirmed",
              "canAnalyze"
            ],
            "properties": {
              "isSuccess": {
                "type": "boolean"
              },
              "ocrRecordId": {
                "type": [
                  "number",
                  "null"
                ]
              },
              "problemText": {
                "type": "string"
              },
              "studentSolutionText": {
                "type": "string"
              },
              "detectedSections": {
                "type": "array",
                "items": {
                  "type": "object"
                }
              },
              "formulas": {
                "type": "array",
                "items": {
                  "type": "object"
                }
              },
              "warnings": {
                "type": "array",
                "items": {
                  "type": "string"
                }
              },
              "reviewReasons": {
                "type": "array",
                "items": {
                  "type": "string"
                }
              },
              "status": {
                "type": "string"
              },
              "needsManualReview": {
                "type": "boolean"
              },
              "isConfirmed": {
                "type": "boolean"
              },
              "canAnalyze": {
                "type": "boolean"
              }
            }
          }
        },
        "photoOcrConfirm": {
          "endpointTemplate": "/api/photo-solutions/ocr/{id}/confirm",
          "method": "POST",
          "description": "Confirms OCR result before analysis.",
          "requestSchema": {
            "type": "object",
            "required": [
              "problemText",
              "formulas"
            ],
            "properties": {
              "problemText": {
                "type": "string"
              },
              "studentSolutionText": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "formulas": {
                "type": "array",
                "items": {
                  "type": "object"
                }
              }
            }
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "isSuccess",
              "problemText",
              "studentSolutionText",
              "formulas",
              "status",
              "isConfirmed",
              "canAnalyze"
            ],
            "properties": {
              "isSuccess": {
                "type": "boolean"
              },
              "problemText": {
                "type": "string"
              },
              "studentSolutionText": {
                "type": "string"
              },
              "formulas": {
                "type": "array",
                "items": {
                  "type": "object"
                }
              },
              "status": {
                "type": "string"
              },
              "isConfirmed": {
                "type": "boolean"
              },
              "canAnalyze": {
                "type": "boolean"
              }
            }
          }
        },
        "symbolicCompute": {
          "endpoint": "/api/symbolic/compute",
          "method": "POST",
          "description": "Runs symbolic compute for admin dev tools.",
          "requestSchema": {
            "type": "object",
            "required": [
              "operation",
              "expression"
            ],
            "properties": {
              "operation": {
                "type": "string"
              },
              "expression": {
                "type": "string"
              },
              "variable": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "lower": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "upper": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "point": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "order": {
                "type": [
                  "number",
                  "null"
                ]
              }
            }
          },
          "responseSchema": {
            "type": "object",
            "required": [
              "success",
              "warnings",
              "elapsedMs"
            ],
            "properties": {
              "success": {
                "type": "boolean"
              },
              "resultText": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "resultLatex": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "warnings": {
                "type": "array",
                "items": {
                  "type": "string"
                }
              },
              "errorCode": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "errorMessage": {
                "type": [
                  "string",
                  "null"
                ]
              },
              "elapsedMs": {
                "type": "number"
              }
            }
          }
        }
      }
    }
  }
};
