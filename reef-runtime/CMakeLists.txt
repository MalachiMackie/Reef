cmake_minimum_required(VERSION 4.1)
project(reef-runtime C)


set(CMAKE_C_STANDARD 11)
set(CMAKE_VERBOSE_MAKEFILE ON)

add_library(reef-runtime STATIC reef-runtime.c)

set_target_properties(reef-runtime PROPERTIES
        COMPILE_PDB_NAME reef-runtime
        COMPILE_PDB_OUTPUT_DIRECTORY ${CMAKE_CURRENT_BINARY_DIR}
)