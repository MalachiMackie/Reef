cmake_minimum_required(VERSION 4.1)
project(reef_runtime C)

set(CMAKE_C_STANDARD 11)

add_library(reef_runtime
        STATIC
        runtime.c)
