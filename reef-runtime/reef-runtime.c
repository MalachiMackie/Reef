#include <assert.h>
#include <stdalign.h>
#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>

#ifdef _MSC_VER
typedef double max_align_t;
#endif

typedef uint8_t *VariantInfoHandle;
typedef uint8_t *FieldInfoHandle;

typedef struct {
    uint8_t variantIdentifier;
} TypeInfo;
typedef TypeInfo *TypeInfoHandle;

extern TypeInfo typeInfoArray[];

extern uint64_t typeInfoCount;
extern uint64_t typeInfoSize;
extern uint64_t fieldInfoSize;
extern uint64_t variantInfoSize;

typedef struct {
	size_t length;
	const char *start;
} string;

TypeInfoHandle get_type_info(uint64_t index)
{
    assert(index < typeInfoCount);
    TypeInfoHandle handle = typeInfoArray;


    return (TypeInfoHandle)(((char*)handle) + (typeInfoSize * index));
}

uint32_t *get_pointer_pointer_to_type_id(TypeInfoHandle handle)
{
    return (uint32_t *)((char*)handle + 4);
}

string *get_class_name(TypeInfoHandle handle)
{
    return (string *)((char*)handle + 8);
}

string *get_union_name(TypeInfoHandle handle)
{
    return (string *)((char*)handle + 8);
}

uint32_t *get_class_type_id(TypeInfoHandle handle)
{
    return (uint32_t *)((char*)handle + 24);
}

uint32_t *get_union_type_id(TypeInfoHandle handle)
{
    return (uint32_t *)((char*)handle + 24);
}

uint16_t get_variant_identifier(TypeInfoHandle handle)
{
    uint16_t *variantIdentifier = (uint16_t*)handle;

    return *variantIdentifier;
}

void print_string(string str)
{
	fputs(str.start, stdout);
}

string get_type_info_name(TypeInfoHandle handle)
{
    switch (get_variant_identifier(handle))
    {
        case 0: // class
            return *get_class_name(handle);
        case 1: // union
            return *get_union_name(handle);
        case 2: // pointer
        {
            uint32_t *pointer_to_type_id = get_pointer_pointer_to_type_id(handle);
            TypeInfoHandle pointer_to_handle = get_type_info(*pointer_to_type_id);
            string value;
            value.length = 6;
            value.start = "*thing";
            return value;
        }
        case 3: // array
        {
            string value;
            value.length = 10;
            value.start = "[thing; 1]";
        }
        default:
            assert(0);
    }
}

#define DEFINE_PRINT_INT(func_name, int_type)                            \
void func_name(int_type num)                                             \
{                                                                        \
    /* Handle negative numbers safely */                                 \
    if (num < 0)                                                         \
    {                                                                    \
        putchar('-');                                                    \
                                                                         \
        /* Convert to positive without overflow */                       \
        int_type n = -(num + 1);                                         \
        n += 1;                                                          \
        num = n;                                                         \
    }                                                                    \
                                                                         \
    if (num > 9)                                                         \
    {                                                                    \
        int_type a = num / 10;                                           \
        num -= a * 10;                                                   \
        func_name(a);                                                    \
    }                                                                    \
                                                                         \
    putchar('0' + (int)num);                                             \
}

DEFINE_PRINT_INT(print_i8, int8_t)
DEFINE_PRINT_INT(print_i16, int16_t)
DEFINE_PRINT_INT(print_i32, int32_t)
DEFINE_PRINT_INT(print_i64, int64_t)

DEFINE_PRINT_INT(print_u8, uint8_t)
DEFINE_PRINT_INT(print_u16, uint16_t)
DEFINE_PRINT_INT(print_u32, uint32_t)
DEFINE_PRINT_INT(print_u64, uint64_t)

void print_type_info(TypeInfoHandle handle)
{
    switch (get_variant_identifier(handle))
    {
        case 0: // class
        {
            print_string(get_type_info_name(handle));
            fputs(":\n    type: class\n", stdout);
            fputs("    id: ", stdout);
            print_u32(*get_class_type_id(handle));
            fputs("\n", stdout);
            break;
        }
        case 1: // union
        {
            print_string(get_type_info_name(handle));
            fputs(":\n    type: union\n", stdout);
            fputs("    id: ", stdout);
            print_u32(*get_union_type_id(handle));
            fputs("\n", stdout);
            break;
        }
        case 2: // pointer
        {
            uint32_t *pointer_to_type_id = get_pointer_pointer_to_type_id(handle);
            TypeInfoHandle pointer_to_handle = get_type_info(*pointer_to_type_id);
            print_string(get_type_info_name(handle));
            fputs(":\n    type: pointer\n", stdout);
            break;
        }
        case 3: // array
        {
            print_string(get_type_info_name(handle));
            fputs(":\n    type: array\n", stdout);
            break;
        }
    }
}

size_t memory_used_bytes = 0;

size_t heap_size_bytes = 1024 * 10; // 10MB

typedef struct {
    void* ptr;
    size_t size;
    uint8_t allocation_space;
} Allocation;

typedef struct {
    Allocation* allocations;
    size_t count;
    size_t size;
} AllocationsList;

typedef struct {
    void* base_addr;
    void* top;
    size_t size;
    AllocationsList allocations;
} Heap;

Heap* heaps;
size_t heaps_count;

void init_runtime() {
    heaps_count = 1;
    heaps = malloc(heaps_count * sizeof(Heap));
    assert(heaps);

    AllocationsList allocations;

    allocations.size = 10;
    allocations.count = 0;
    allocations.allocations = malloc(allocations.size * sizeof(Allocation));
    assert(allocations.allocations);

    Heap heap;
    heap.size = heap_size_bytes;
    heap.base_addr = malloc(heap.size);
    heap.top = heap.base_addr;
    assert(heap.base_addr);
    heap.allocations = allocations;

    heaps[0] = heap;
    for (uint64_t i = 0; i < typeInfoCount; i++)
    {
        // print_type_info(get_type_info(i));
    }
}

void* allocate(size_t size) {
    size_t unaligned_new_top = (size_t)heaps[0].top + size;
    size_t new_top = unaligned_new_top + (unaligned_new_top % alignof(max_align_t));

    // todo: if no more space in heap, allocate a new one
    assert(new_top < (size_t)heaps[0].base_addr + heaps[0].size);
    heaps[0].top = (void*)new_top;
    AllocationsList* allocations = &heaps[0].allocations;


    // todo: if no more allocations, allocate a new one
    assert(allocations->count < allocations->size);

    Allocation allocation;
    allocation.ptr = (void*)new_top;
    allocation.size = size;
    allocation.allocation_space = (uint8_t)(new_top - unaligned_new_top);

    allocations->allocations[allocations->count++] = allocation;

    return (void*)new_top;
}

void trigger_gc() {

}

size_t get_memory_usage_bytes() {
    return memory_used_bytes;
}
